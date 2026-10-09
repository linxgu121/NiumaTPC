using System.Collections.Generic;
using FishNet.Object;
using FishNet.Utility.Template;
using NiumaTPC.Item;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NiumaTPC.FishNet
{
    /// <summary>
    /// 一个物理世界中的服务器弹道驱动
    /// 只推进逻辑子弹，不生成客户端视觉对象
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public sealed class AmmunitionDriver : TickNetworkBehaviour
    {
        #region Inspector

        [SerializeField]
        [Tooltip("弹道能够击中的层，包含墙体、地形和目标碰撞体")]
        private LayerMask _hitMask = Physics.DefaultRaycastLayers;

        [SerializeField]
        [Tooltip("普通实体碰撞体使用 Ignore；目标受击框是 Trigger 时使用 Collide")]
        private QueryTriggerInteraction _triggerInteraction =
            QueryTriggerInteraction.Ignore;

        [SerializeField]
        [Tooltip("打印子弹命中、到期或取消结果，正式运行时可以关闭")]
        private bool _logEnds = true;

        #endregion

        #region 运行时状态

        // 同一个物理世界只允许一个服务器弹道驱动
        private static readonly
            Dictionary<PhysicsScene, AmmunitionDriver>
            ServerWorlds = new();

        private PhysicsScene _physicsScene;
        private AmmunitionWorld _world;
        private bool _registered;

        private struct ServerShotInfo
        {
            public string DefinitionId;
            public ShotRequestKey RequestKey;
        }

        // 保存发射时的资源与来源身份
        // 命中时不能再读取射手当前装备
        private readonly Dictionary<ulong, ServerShotInfo> _serverShots = new();
        private AmmunitionPresenter _presenter;

        private bool IsReady =>
            isActiveAndEnabled &&
            IsServerInitialized &&
            _registered &&
            _world != null;

        #endregion

        #region 生命周期

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRegistry()
        {
            // 兼容编辑器关闭 Domain Reload 的运行方式
            ServerWorlds.Clear();
        }

        private void Awake()
        {
            _presenter = GetComponent<AmmunitionPresenter>();

            // 所有角色完成 OnTick 移动后，再统一推进弹道
            SetTickCallbacks(TickCallback.PostTick);
        }

        public override void OnStartServer()
        {
            base.OnStartServer();

            ReleaseWorld();

            if (!isActiveAndEnabled)
            {
                return;
            }

            _physicsScene = gameObject.scene.GetPhysicsScene();

            if (!_physicsScene.IsValid())
            {
                Debug.LogError(
                    "[网络弹道] 无法取得有效的物理世界",
                    this);
                return;
            }

            if (ServerWorlds.TryGetValue(
                    _physicsScene,
                    out AmmunitionDriver existing) &&
                existing != null &&
                existing != this)
            {
                Debug.LogError(
                    "[网络弹道] 同一物理世界已经存在弹道驱动，" +
                    "请移除重复组件或对象",
                    this);
                return;
            }

            _world = new AmmunitionWorld(
                _physicsScene,
                _hitMask,
                _triggerInteraction);

            ServerWorlds[_physicsScene] = this;
            _registered = true;

            Debug.Log("[网络弹道] 服务器弹道世界已就绪", this);
        }

        public override void OnStopServer()
        {
            ReleaseWorld();
            base.OnStopServer();
        }

        public override void OnStartClient()
        {
            base.OnStartClient();

            if (_presenter == null)
            {
                Debug.LogError("[网络弹道] 同一对象上缺少 NiumaFishNetAmmunitionPresenter", this);
                return;
            }

            _presenter.BeginSession();
        }

        public override void OnStopClient()
        {
            if (_presenter != null)
            {
                _presenter.EndSession();
            }

            base.OnStopClient();
        }

        private void OnDestroy()
        {
            SetTickCallbacks(TickCallback.None);
            ReleaseWorld();
        }

        private void ReleaseWorld()
        {
            // 只移除自己的登记，不影响其他驱动
            if (_registered &&
                ServerWorlds.TryGetValue(
                    _physicsScene,
                    out AmmunitionDriver current) &&
                current == this)
            {
                ServerWorlds.Remove(_physicsScene);
            }

            _registered = false;
            _serverShots.Clear();

            _world?.Clear();
            _world = null;
            _physicsScene = default;
        }

        #endregion

        #region 服务器发射入口

        internal static bool TryGetForScene(
            Scene scene,
            out AmmunitionDriver driver)
        {
            driver = null;

            if (!scene.IsValid() || !scene.isLoaded)
            {
                return false;
            }

            PhysicsScene physicsScene = scene.GetPhysicsScene();

            if (!physicsScene.IsValid() ||
                !ServerWorlds.TryGetValue(
                    physicsScene,
                    out AmmunitionDriver candidate) ||
                candidate == null ||
                !candidate.IsReady)
            {
                return false;
            }

            driver = candidate;
            return true;
        }

        /// <summary>
        /// 只允许服务器武器流程传入已经验证的射线
        /// 此方法不扣弹、不播放表现、不调用外部事件
        /// </summary>
        internal bool TrySpawn(
            RangedWeaponSO weapon,
            Ray approvedAim,
            Transform shooterRoot,
            ShotRequestKey requestKey,
            out ulong shotId)
        {
            shotId = 0UL;

            if (!IsReady ||
                weapon == null ||
                weapon.Ammunition == null ||
                shooterRoot == null ||
                !requestKey.IsValid)
            {
                return false;
            }

            // 不允许把另一个物理世界的射手登记到这里
            if (shooterRoot.gameObject.scene.GetPhysicsScene() !=
                _physicsScene)
            {
                return false;
            }

            AmmunitionDefinitionSO ammunition = weapon.Ammunition;

            // 网络端需要通过稳定 ID 找到对应的本地表现资源
            if (string.IsNullOrWhiteSpace(ammunition.DefinitionId))
            {
                Debug.LogError("[网络弹道] 弹药缺少 DefinitionId", ammunition);
                return false;
            }

            // approvedAim 已经过方向归一化和服务器视点检查
            Vector3 initialVelocity = approvedAim.direction * weapon.ProjectileSpeed;

            Vector3 acceleration = Physics.gravity * ammunition.GravityScale;

            if (!_world.TrySpawn(
                approvedAim.origin,
                initialVelocity,
                acceleration,
                ammunition.CollisionRadius,
                ammunition.MaxLifetime,
                shooterRoot,
                out shotId))
            {
                return false;
            }

            _serverShots.Add(shotId, new ServerShotInfo
            {
                DefinitionId = ammunition.DefinitionId,
                RequestKey = requestKey
            });

            return true;
        }

        /// <summary>
        /// 只能在服务器登记成功且扣弹提交后调用
        /// </summary>
        internal void PublishAcceptedShot(ulong shotId)
        {
            if (!IsReady ||
                !_serverShots.TryGetValue(shotId, out ServerShotInfo shotInfo) ||
                !_world.TryGetState(shotId, out AmmunitionState state))
            {
                return;
            }

            ShotRequestKey key = shotInfo.RequestKey;

            ObserveShotSpawn(
                shotId,
                shotInfo.DefinitionId,
                key.ServerWeaponInstanceId,
                key.EquipmentRevision,
                key.RequestSequence,
                state.StartPosition,
                state.Velocity,
                state.Acceleration,
                state.MaxLifetime,
                TimeManager.Tick);
        }

        #endregion

        #region 服务器 Tick

        protected override void TimeManager_OnPostTick()
        {
            if (!IsReady || _world.ActiveCount == 0)
            {
                return;
            }

            // 同步角色已经完成的 Transform 更改
            // 不调用 Physics.Simulate，避免重复推进物理
            Physics.SyncTransforms();

            IReadOnlyList<AmmunitionEndResult> ended =
                _world.SimulateTick((float)TimeManager.TickDelta);

            // 结果列表会被下次模拟复用，必须在这里立即消费
            for (int i = 0; i < ended.Count; i++)
            {
                HandleEnded(ended[i]);
            }
        }

        private void HandleEnded(AmmunitionEndResult result)
        {
            AmmunitionState state = result.State;

            // 清理和广播不能受日志开关影响
            if (_serverShots.TryGetValue(
                    state.ShotId,
                    out ServerShotInfo shotInfo))
            {
                _serverShots.Remove(state.ShotId);

                Vector3 point = result.HasHit
                    ? result.Hit.point
                    : state.Position;

                Vector3 normal = result.HasHit
                    ? result.Hit.normal
                    : Vector3.zero;

                ShotRequestKey key = shotInfo.RequestKey;

                ObserveShotEnd(
                    state.ShotId,
                    shotInfo.DefinitionId,
                    key.ServerWeaponInstanceId,
                    key.EquipmentRevision,
                    key.RequestSequence,
                    result.HasHit,
                    point,
                    normal);
            }

            if (!_logEnds)
            {
                return;
            }

            string targetName =
                result.HasHit && result.Hit.collider != null
                    ? result.Hit.collider.name
                    : "-";

            Debug.Log(
                $"[网络弹道/结束] ShotId={state.ShotId}, " +
                $"WeaponInstance={shotInfo.RequestKey.ServerWeaponInstanceId}, " +
                $"EquipmentRevision={shotInfo.RequestKey.EquipmentRevision}, " +
                $"Request={shotInfo.RequestKey.RequestSequence}, " +
                $"ServerTick={TimeManager.Tick}, " +
                $"Status={state.Status}, HasHit={result.HasHit}, " +
                $"Target={targetName}, Age={state.Age:F3}, " +
                $"Position={state.Position}",
                this);
        }

        #endregion

        #region 客户端广播接收

        [ObserversRpc(
    RunLocally = false,
    BufferLast = false,
    ExcludeOwner = false,
    ExcludeServer = false)]
        private void ObserveShotSpawn(
    ulong shotId,
    string definitionId,
    string serverWeaponInstanceId,
    uint equipmentRevision,
    ulong requestSequence,
    Vector3 origin,
    Vector3 initialVelocity,
    Vector3 acceleration,
    float lifetime,
    uint serverTick)
        {
            if (!IsClientInitialized || _presenter == null)
            {
                return;
            }

            ShotRequestKey requestKey = new(
                serverWeaponInstanceId,
                equipmentRevision,
                requestSequence);

            // Tick 可能回绕，不能直接做无符号年龄减法
            int elapsedTicks = unchecked((int)(TimeManager.Tick - serverTick));

            float initialAge = Mathf.Clamp(
                elapsedTicks * (float)TimeManager.TickDelta,
                0f,
                lifetime);

            _presenter.PlaySpawn(
                shotId,
                definitionId,
                requestKey,
                origin,
                initialVelocity,
                acceleration,
                lifetime,
                initialAge);
        }

        [ObserversRpc(
     RunLocally = false,
     BufferLast = false,
     ExcludeOwner = false,
     ExcludeServer = false)]
        private void ObserveShotEnd(
     ulong shotId,
     string definitionId,
     string serverWeaponInstanceId,
     uint equipmentRevision,
     ulong requestSequence,
     bool hasHit,
     Vector3 point,
     Vector3 normal)
        {
            if (!IsClientInitialized || _presenter == null)
            {
                return;
            }

            ShotRequestKey requestKey = new(
                serverWeaponInstanceId,
                equipmentRevision,
                requestSequence);

            _presenter.PlayEnd(
                shotId,
                definitionId,
                requestKey,
                hasHit,
                point,
                normal);
        }

        #endregion
    }
}
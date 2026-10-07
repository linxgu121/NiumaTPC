using System.Collections.Generic;
using NiumaTPC.Core.Object;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NiumaTPC.Item
{
    public class OfflineAmmunitionDriver : MonoBehaviour
    {
        [Header("碰撞查询")]

        [SerializeField]
        [Tooltip("参与弹道检测的层，需要包含墙体和目标所在层")]
        private LayerMask hitMask = Physics.DefaultRaycastLayers;

        [SerializeField]
        [Tooltip("普通实体碰撞体使用 Ignore 目标受击框是 Trigger 时使用 Collide")]
        private QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Ignore;

        [Header("轨迹诊断")]

        [SerializeField]
        [Tooltip("每段调试线保留的秒数，只影响显示，不改变子弹寿命")]
        private float traceDuration = 2f;

        [Header("视觉表现")]

        [SerializeField]
        [Tooltip("绑定场景中的 SimpleObjectPoolSystem 配置了视觉预制体时必须绑定")]
        private SimpleObjectPoolSystem visualPool;

        [SerializeField]
        [Min(0)]
        [Tooltip("同时显示的命中特效上限 满额时回收最早生成的效果 0 表示关闭")]
        private int maxActiveImpacts = 128;

        // 本轮启用期间固定使用这个池，避免运行中修改引用后归还到错误的池
        private SimpleObjectPoolSystem _viewPool;

        // 这里只保存视图关联，模拟状态仍由 AmmunitionWorld 唯一持有
        private readonly Dictionary<ulong, AmmunitionView> _views = new Dictionary<ulong, AmmunitionView>(64);

        private AmmunitionWorld _world;

        // 保存本轮活动编号，用于调试查询与终态去重
        private readonly HashSet<ulong> _shotIds = new HashSet<ulong>();

        #region 命中特效运行状态

        // 发射时记录资源引用，避免飞行途中更换弹药影响旧子弹表现
        // 发射时保存这次使用的资源，避免途中换枪影响旧子弹
        private readonly Dictionary<ulong, ImpactResources> _impactResources = new Dictionary<ulong, ImpactResources>(64);

        private readonly struct ImpactResources
        {
            public readonly GameObject Prefab;
            public readonly AudioClip Sound;

            public ImpactResources(GameObject prefab, AudioClip sound)
            {
                Prefab = prefab;
                Sound = sound;
            }
        }

        private readonly List<ActiveImpact> _activeImpacts = new List<ActiveImpact>(32);

        private readonly struct ActiveImpact
        {
            public readonly AmmunitionImpactView View;
            public readonly double ExpiresAt;

            public ActiveImpact(
                AmmunitionImpactView view,
                double expiresAt)
            {
                View = view;
                ExpiresAt = expiresAt;
            }
        }

        #endregion

        #region Unity 生命周期

        private void OnEnable()
        {
            _viewPool = visualPool;
            // 使用当前组件所属场景的物理世界
            PhysicsScene physicsScene = gameObject.scene.GetPhysicsScene();

            _world = new AmmunitionWorld(
                physicsScene,
                hitMask,
                triggerInteraction);
        }

        private void Update()
        {
            // 即使已经没有飞行中的子弹，命中特效仍需要计时回收
            UpdateImpactViews();
        }

        private void FixedUpdate()
        {
            if (_world == null || _world.ActiveCount == 0)
            {
                return;
            }

            // 每批查询前同步一次已经发生的 Transform 更改
            // 这不会推进刚体，也不代表角色和子弹已经统一了模拟时钟
            Physics.SyncTransforms();

            IReadOnlyList<AmmunitionEndResult> ended = _world.SimulateTick(Time.fixedDeltaTime);

            // 必须在下一次 SimulateTick 或 Clear 前消费结果
            for (int i = 0; i < ended.Count; i++)
            {
                HandleEnded(ended[i]);
            }

            // 终态编号已经移除，这里只读取仍在飞行的子弹
            foreach (ulong shotId in _shotIds)
            {
                if (_world.TryGetState(shotId, out AmmunitionState state))
                {
                    DrawSegment(in state, Color.green);
                }
            }
        }

        private void LateUpdate()
        {
            if (_world == null || _views.Count == 0)
            {
                return;
            }

            // 计算当前渲染帧处于两个固定更新之间的比例
            float alpha = Mathf.Clamp01(
                (float)(
                    (Time.timeAsDouble - Time.fixedTimeAsDouble) /
                    Time.fixedDeltaTime));

            foreach (KeyValuePair<ulong, AmmunitionView> pair in _views)
            {
                AmmunitionView view = pair.Value;

                if (view == null || !_world.TryGetState(pair.Key, out AmmunitionState state))
                {
                    continue;
                }

                // 只平滑显示，不修改世界中的真实位置
                Vector3 displayPosition = Vector3.Lerp(
                    state.PreviousPosition,
                    state.Position,
                    alpha);

                view.ApplyPose(pair.Key, displayPosition, state.Velocity);
            }
        }

        private void OnDisable()
        {
            // 归还对象期间必须保留原对象池引用
            ReleaseAllViews();
            ReleaseAllImpacts();

            _world?.Clear();
            _world = null;

            _shotIds.Clear();
            _impactResources.Clear();

            _viewPool = null;
        }

        #endregion

        #region 发射入口

        /// <summary>
        /// 登记一颗离线逻辑子弹
        /// 起点和方向由调用方提供，不读取模型枪口方向
        /// 返回 true 只表示登记成功，起点碰撞检查仍在后续 Tick 执行
        /// </summary>
        public bool TryFire(
            RangedWeaponSO weapon,
            Vector3 origin,
            Vector3 direction,
            Transform shooter,
            out ulong shotId)
        {
            shotId = 0;

            if (!Application.isPlaying ||
                !isActiveAndEnabled ||
                _world == null)
            {
                return false;
            }

            if (weapon == null || weapon.Ammunition == null)
            {
                Debug.LogError("[离线弹道] 发射失败，缺少武器或弹药配置", this);

                return false;
            }

            if (direction.sqrMagnitude <= 0.000001f)
            {
                Debug.LogWarning("[离线弹道] 发射失败，瞄准方向为零", this);

                return false;
            }

            AmmunitionDefinitionSO ammunition = weapon.Ammunition;

            Vector3 initialVelocity = direction.normalized * weapon.ProjectileSpeed;

            Vector3 acceleration = Physics.gravity * ammunition.GravityScale;

            if (!_world.TrySpawn(
                origin,
                initialVelocity,
                acceleration,
                ammunition.CollisionRadius,
                ammunition.MaxLifetime,
                shooter,
                out shotId))
            {
                return false;
            }

            _shotIds.Add(shotId);

            // 保存这次发射的表现资源，后续换枪不影响旧子弹
            if (ammunition.ImpactPrefab != null || ammunition.ImpactSound != null)
            {
                _impactResources.Add(shotId, new ImpactResources(ammunition.ImpactPrefab, ammunition.ImpactSound));
            }

            SpawnView(ammunition.VisualPrefab, shotId, origin, initialVelocity);

            return true;
        }


        #endregion

        #region 子弹视觉生命周期

        private void SpawnView(GameObject prefab, ulong shotId, Vector3 position, Vector3 velocity)
        {

            // 允许没有视觉资源，只运行弹道和调试线
            if (prefab == null)
            {
                return;
            }

            if (_viewPool == null)
            {
                Debug.LogError(
                    "[离线弹道] 已配置 VisualPrefab，但没有绑定视觉对象池",
                    this);

                return;
            }

            GameObject instance = _viewPool.Spawn(prefab);

            if (instance == null)
            {
                return;
            }

            // 子弹使用世界空间，不能跟着角色或枪口一起移动
            instance.transform.SetParent(null, true);

            // 对象池可能位于其他场景，视图归当前驱动所在场景管理
            if (instance.scene != gameObject.scene)
            {
                SceneManager.MoveGameObjectToScene(
                    instance,
                    gameObject.scene);
            }

            if (!instance.TryGetComponent(out AmmunitionView view))
            {
                Debug.LogError(
                    "[离线弹道] VisualPrefab 根节点缺少 AmmunitionView",
                    instance);

                _viewPool.Despawn(instance);
                return;
            }

            // 对象池只负责取出，取出后立即绑定本次子弹身份
            if (!view.Bind(shotId, position, velocity))
            {
                Debug.LogError(
                    $"[离线弹道] 视觉绑定失败：ShotId={shotId}",
                    instance);

                _viewPool.Despawn(instance);
                return;
            }

            _views.Add(shotId, view);
        }

        private void ReleaseView(ulong shotId)
        {
            if (!_views.TryGetValue(shotId, out AmmunitionView view))
            {
                return;
            }

            // 先移除关联，避免同一结束通知重复归还对象
            _views.Remove(shotId);

            ReturnView(view);
        }

        private void ReleaseAllViews()
        {
            foreach (AmmunitionView view in _views.Values)
            {
                ReturnView(view);
            }

            _views.Clear();
        }

        private void ReturnView(AmmunitionView view)
        {
            if (view == null)
            {
                return;
            }

            if (_viewPool != null)
            {
                _viewPool.Despawn(view.gameObject);
            }
            else
            {
                // 停机时池可能先被销毁，清理本驱动仍持有的孤立视图
                Destroy(view.gameObject);
            }
        }

        #endregion

        #region 命中特效生命周期

        private void SpawnImpact(ImpactResources resources, Vector3 point, Vector3 normal)
        {
            GameObject prefab = resources.Prefab;

            if (prefab == null || maxActiveImpacts <= 0)
            {
                return;
            }

            if (_viewPool == null)
            {
                Debug.LogError(
                    "[离线弹道] 已配置命中特效，但没有绑定视觉对象池",
                    this);

                return;
            }

            // 先清理已经到期的效果，再处理显示预算
            UpdateImpactViews();

            // 列表按生成顺序排列，满额时优先回收最早的效果
            while (_activeImpacts.Count >= maxActiveImpacts)
            {
                ReleaseImpactAt(0);
            }

            GameObject instance = _viewPool.Spawn(prefab);

            if (instance == null)
            {
                return;
            }

            // 命中特效独立存在，不能挂在即将回收的飞行子弹下
            instance.transform.SetParent(null, true);

            if (instance.scene != gameObject.scene)
            {
                SceneManager.MoveGameObjectToScene(
                    instance,
                    gameObject.scene);
            }

            if (!instance.TryGetComponent(out AmmunitionImpactView view))
            {
                Debug.LogError(
                    "[离线弹道] ImpactPrefab 根节点缺少 AmmunitionImpactView",
                    instance);

                _viewPool.Despawn(instance);
                return;
            }

            if (!view.PlayAt(point, normal))
            {
                Debug.LogError(
                    "[离线弹道] 命中特效播放失败，请检查组件启用状态与命中法线",
                    instance);

                _viewPool.Despawn(instance);
                return;
            }

            // 使用本次播放时的保留时长，运行中修改配置不会改变这次截止时间
            double expiresAt = Time.timeAsDouble + view.RetainTime;

            _activeImpacts.Add(new ActiveImpact(view, expiresAt));
        }

        private void UpdateImpactViews()
        {
            if (_activeImpacts.Count == 0)
            {
                return;
            }

            double now = Time.timeAsDouble;

            // 倒序移除，避免删除元素后跳过相邻条目
            for (int i = _activeImpacts.Count - 1; i >= 0; i--)
            {
                ActiveImpact impact = _activeImpacts[i];

                if (impact.View == null ||
                    !impact.View.isActiveAndEnabled ||
                    !impact.View.IsPlaying ||
                    now >= impact.ExpiresAt)
                {
                    ReleaseImpactAt(i);
                }
            }

            // 支持运行中降低上限，设为 0 时清空已有命中特效
            int limit = Mathf.Max(0, maxActiveImpacts);

            while (_activeImpacts.Count > limit)
            {
                ReleaseImpactAt(0);
            }
        }

        private void ReleaseImpactAt(int index)
        {
            AmmunitionImpactView view = _activeImpacts[index].View;

            // 先移除管理记录，再触发对象池回调
            _activeImpacts.RemoveAt(index);

            if (view == null)
            {
                return;
            }

            if (_viewPool != null)
            {
                _viewPool.Despawn(view.gameObject);
            }
            else
            {
                // 池先被销毁时，清理本驱动仍持有的孤立实例
                Destroy(view.gameObject);
            }
        }

        private void ReleaseAllImpacts()
        {
            while (_activeImpacts.Count > 0)
            {
                ReleaseImpactAt(_activeImpacts.Count - 1);
            }
        }

        #endregion

        private void HandleEnded(AmmunitionEndResult result)
        {
            AmmunitionState state = result.State;

            // 同一本地子弹的终态只处理一次
            if (!_shotIds.Remove(state.ShotId))
            {
                return;
            }

            ReleaseView(state.ShotId);

            _impactResources.TryGetValue(
    state.ShotId,
    out ImpactResources resources);

            // 命中、到期和取消，都必须清掉本次资源记录
            _impactResources.Remove(state.ShotId);

            Color color = result.HasHit
                ? Color.red
                : state.Status == AmmunitionStatus.Expired
                    ? Color.yellow
                    : Color.magenta;

            // 已结束的子弹不在活动集合中，使用结果里的终态快照
            DrawSegment(in state, color);

            string targetName = "-";

            if (result.HasHit)
            {
                // 命中短音效独立播放，不覆盖特效自身的循环声音
                if (resources.Sound != null)
                {
                    AudioSource.PlayClipAtPoint(resources.Sound, result.Hit.point);
                }

                SpawnImpact(resources, result.Hit.point, result.Hit.normal);

                if (result.Hit.collider != null)
                {
                    targetName = result.Hit.collider.name;
                }

                // 接触点与法线来自真实查询结果
                Debug.DrawRay(
                    result.Hit.point,
                    result.Hit.normal * 0.2f,
                    Color.red,
                    traceDuration,
                    false);
            }

            Debug.Log(
                $"[离线弹道] 结束：ShotId={state.ShotId}，" +
                $"Status={state.Status}，Target={targetName}，" +
                $"Age={state.Age:F3}s，" +
                $"Distance={state.TravelledDistance:F2}m，" +
                $"Active={_world.ActiveCount}",
                this);
        }

        private void DrawSegment(
            in AmmunitionState state,
            Color color)
        {
            // 仅连接本 Tick 起末位置，不展示内部每一个曲线子步
            Debug.DrawLine(
                state.PreviousPosition,
                state.Position,
                color,
                traceDuration,
                false);
        }



    }
}

using System;
using FishNet.Utility.Template;
using NiumaTPC.Core.Combat;
using NiumaTPC.Enemy.Brain;
using UnityEngine;

namespace NiumaTPC.FishNet.Enemy
{
    /// <summary>
    /// 由服务器管理敌人死亡后的尸体保留与网络回收
    /// 死亡表现由角色状态机负责，本组件不播放动画
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EnemyBrain))]
    public sealed class ServerEnemyDeathLifecycle : TickNetworkBehaviour, IServerEnemyDeathSource
    {
        #region Inspector(面板配置)

        [SerializeField]
        [Tooltip("当前敌人的EnemyBrain，未绑定时自动获取")]
        private EnemyBrain brain;

        [SerializeField]
        [Min(0f)]
        [Tooltip("敌人死亡后在场景中保留的秒数，0表示检测到死亡后立即回收")]
        private float corpseLifetimeSeconds = 3f;

        #endregion

        #region Runtime State(运行时状态)

        private bool _deathObserved;
        private bool _despawnRequested;
        private uint _despawnTick;

        #endregion

        #region Events(事件)

        public Component SourceComponent => this;

        /// <summary>
        /// 服务器确认该敌人死亡时触发一次
        /// 经验、掉落和关卡计数系统可以订阅该事件
        /// </summary>
        public event Action<CharacterDeathInfo> OnServerDied;

        #endregion

        #region Unity Lifecycle(Unity 生命周期)

        private void Reset()
        {
            brain = GetComponent<EnemyBrain>();
        }

        private void Awake()
        {
            SetTickCallbacks(TickCallback.Tick);
            if (brain == null) brain = GetComponent<EnemyBrain>();
        }

        private void OnValidate()
        {
            corpseLifetimeSeconds = Mathf.Max(0f, corpseLifetimeSeconds);
        }

        #endregion

        #region FishNet Lifecycle(FishNet 生命周期)

        public override void OnStartServer()
        {
            base.OnStartServer();

            if (brain == null) brain = GetComponent<EnemyBrain>();

            _deathObserved = false;
            _despawnRequested = false;
            _despawnTick = 0u;

            if (brain == null || brain.Character == null)
            {
                Debug.LogError("[敌人死亡] 缺少EnemyBrain或角色控制器，无法管理服务器死亡回收", this);
                return;
            }

            brain.Character.OnDied -= HandleCharacterDied;
            brain.Character.OnDied += HandleCharacterDied;
        }
        public override void OnStopServer()
        {
            if (brain != null && brain.Character != null)
            {
                brain.Character.OnDied -= HandleCharacterDied;
            }

            _deathObserved = false;
            _despawnRequested = false;
            _despawnTick = 0u;

            base.OnStopServer();
        }

        #endregion

        #region Server Tick(服务器固定Tick)

        protected override void TimeManager_OnTick()
        {
            if (!IsServerInitialized || _despawnRequested || !_deathObserved) return;
            if (!HasReachedTick(TimeManager.LocalTick, _despawnTick)) return;

            _despawnRequested = true;
            Despawn();
        }

        /// <summary>
        /// 接收角色生命仲裁器确认的死亡结果并安排服务器回收
        /// </summary>
        private void HandleCharacterDied(CharacterDeathInfo deathInfo)
        {
            if (!IsServerInitialized || _deathObserved) return;

            _deathObserved = true;
            _despawnTick = unchecked(TimeManager.LocalTick + ConvertSecondsToTicks(corpseLifetimeSeconds));

            OnServerDied?.Invoke(deathInfo);
        }

        #endregion

        #region Helpers(辅助方法)

        private uint ConvertSecondsToTicks(float seconds)
        {
            if (seconds <= 0f || TimeManager.TickRate == 0) return 0u;

            return (uint)Mathf.Max(1, Mathf.CeilToInt(seconds * TimeManager.TickRate));
        }

        /// <summary>
        /// 判断当前Tick是否到达目标Tick并兼容uint自然回绕
        /// </summary>
        private static bool HasReachedTick(uint currentTick, uint targetTick)
        {
            return unchecked((int)(currentTick - targetTick)) >= 0;
        }

        #endregion
    }
}

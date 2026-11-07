using System;
using FishNet.Utility.Template;
using NiumaTPC.Core.Combat;
using NiumaTPC.Enemy.Core;
using UnityEngine;

namespace NiumaTPC.FishNet.Enemy
{
    /// <summary>
    /// 监听普通怪物的服务器权威死亡结果并负责网络回收
    /// 不负责伤害计算、死亡动画或掉落结算
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BasicEnemyActor))]
    public sealed class ServerBasicEnemyDeathLifecycle : TickNetworkBehaviour, IServerEnemyDeathSource
    {
        #region Inspector(面板配置)

        [SerializeField]
        [Tooltip("普通怪物生命组件，未绑定时自动从当前物体获取BasicEnemyActor")]
        private BasicEnemyActor actor;

        [SerializeField]
        [Min(0f)]
        [Tooltip("死亡后保留时间，0表示下一个服务器Tick立即回收")]
        private float corpseLifetimeSeconds;

        #endregion

        #region Runtime State(运行时状态)

        private bool _deathObserved;
        private bool _despawnRequested;
        private uint _despawnTick;

        #endregion

        #region Events(事件)

        public Component SourceComponent => this;

        /// <summary>
        /// 服务器首次确认普通怪物死亡时触发
        /// 后续掉落、经验和波次系统可以订阅该事件
        /// </summary>
        public event Action<CharacterDeathInfo> OnServerDied;

        #endregion

        #region Unity Lifecycle(Unity 生命周期)

        private void Reset()
        {
            actor = GetComponent<BasicEnemyActor>();
        }

        private void Awake()
        {
            SetTickCallbacks(TickCallback.Tick);
            if (actor == null) actor = GetComponent<BasicEnemyActor>();
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

            if (actor == null) actor = GetComponent<BasicEnemyActor>();

            _deathObserved = false;
            _despawnRequested = false;
            _despawnTick = 0u;

            if (actor == null || !actor.IsInitialized)
            {
                Debug.LogError("[普通怪物死亡] BasicEnemyActor缺失或初始化失败，无法管理服务器死亡回收", this);
                return;
            }

            actor.ResetRuntimeState();
            actor.OnDied -= HandleActorDied;
            actor.OnDied += HandleActorDied;
        }

        public override void OnStopServer()
        {
            if (actor != null) actor.OnDied -= HandleActorDied;

            _deathObserved = false;
            _despawnRequested = false;
            _despawnTick = 0u;

            base.OnStopServer();
        }

        #endregion

        #region Server Tick(服务器固定Tick)

        protected override void TimeManager_OnTick()
        {
            if (!IsServerInitialized || !_deathObserved || _despawnRequested) return;
            if (!HasReachedTick(TimeManager.LocalTick, _despawnTick)) return;

            _despawnRequested = true;
            Despawn();
        }

        /// <summary>
        /// 接收普通怪物生命归零事件并安排服务器网络回收
        /// 重复死亡事件不会重复触发掉落或回收
        /// </summary>
        private void HandleActorDied(CharacterDeathInfo deathInfo)
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

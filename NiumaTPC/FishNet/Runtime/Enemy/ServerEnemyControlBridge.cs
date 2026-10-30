using FishNet.Object;
using NiumaTPC.Enemy.Brain;
using UnityEngine;

namespace NiumaTPC.FishNet.Enemy
{
    /// <summary>
    /// 连接FishNet服务器生命周期与EnemyBrain
    /// 只允许服务器生成敌人AI决策
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EnemyBrain))]
    [RequireComponent(typeof(NiumaFishNetPredictionDriver))]
    public sealed class ServerEnemyControlBridge : NetworkBehaviour
    {
        #region Inspector(面板配置)

        [SerializeField]
        [Tooltip("当前网络敌人的EnemyBrain")]
        private EnemyBrain brain;

        #endregion

        #region Runtime State(运行时状态)

        private bool _serverControlActive;

        #endregion

        #region Public API(公共接口)

        public EnemyBrain Brain => brain;

        public bool IsServerControlActive => _serverControlActive;

        /// <summary>
        /// 检查服务器AI是否具备运行条件并执行一次固定Tick决策
        /// 客户端、未初始化对象和非法Tick间隔不能生成控制意图
        /// </summary>
        public bool TryRunServerTick(uint serverTick, float tickDeltaTime)
        {
            if (!_serverControlActive || !IsServerInitialized || brain == null || brain.RuntimeData == null)
            {
                return false;
            }

            if (float.IsNaN(tickDeltaTime) || float.IsInfinity(tickDeltaTime) || tickDeltaTime <= 0f)
            {
                return false;
            }

            brain.RunServerTick(serverTick, tickDeltaTime);
            return true;
        }

        #endregion

        #region Unity Lifecycle(Unity 生命周期)

        private void Reset()
        {
            brain = GetComponent<EnemyBrain>();
        }

        private void Awake()
        {
            if (brain == null)
            {
                brain = GetComponent<EnemyBrain>();
            }
        }

        #endregion

        #region FishNet Lifecycle(FishNet 生命周期)

        public override void OnStartServer()
        {
            base.OnStartServer();

            if (brain == null || brain.RuntimeData == null)
            {
                Debug.LogError("[敌人网络] EnemyBrain尚未完成初始化，无法取得服务器AI控制权", this);
                return;
            }

            _serverControlActive = true;
            brain.SetServerAuthorityActive(true);
        }

        public override void OnStopServer()
        {
            _serverControlActive = false;

            if (brain != null)
            {
                brain.SetServerAuthorityActive(false);
            }

            base.OnStopServer();
        }

        #endregion
    }
}
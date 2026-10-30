using NiumaTPC.Core.Combat;
using UnityEngine;

namespace NiumaTPC.Enemy.Core
{
    /// <summary>
    /// 保存单个敌人实例的AI运行数据
    /// 不重复保存TPC已经拥有的生命、装备和动作状态
    /// </summary>
    public sealed class EnemyRuntimeData
    {
        #region Decision State

        /// <summary>
        /// 当前高层AI决策状态
        /// </summary>
        public EnemyDecisionState DecisionState;

        /// <summary>
        /// 当前选中的整名角色目标
        /// </summary>
        public IMeleeTarget CurrentTarget;

        /// <summary>
        /// 最近一次确认目标时的位置
        /// </summary>
        public Vector3 LastKnownTargetPosition;

        /// <summary>
        /// 最近一次确认目标有效的服务器Tick
        /// </summary>
        public uint LastSeenTargetTick;

        #endregion

        #region Scheduling

        /// <summary>
        /// 下一次允许重新执行AI决策的服务器Tick
        /// </summary>
        public uint NextDecisionTick;

        /// <summary>
        /// 下一次允许重新计算导航路径的服务器Tick
        /// </summary>
        public uint NextRepathTick;

        /// <summary>
        /// 当前正在跟随的路径拐点下标
        /// -1表示没有可用路径
        /// </summary>
        public int CurrentPathCornerIndex = -1;

        #endregion

        #region Control Intent

        /// <summary>
        /// 当前准备交给TPC输入管线的控制意图
        /// </summary>
        public EnemyControlIntent ControlIntent;

        #endregion

        #region Public API

        /// <summary>
        /// 清理对象池复用时不能继承的AI运行状态
        /// </summary>
        public void Reset()
        {
            DecisionState = EnemyDecisionState.Idle;
            CurrentTarget = null;
            LastKnownTargetPosition = Vector3.zero;
            LastSeenTargetTick = 0u;
            NextDecisionTick = 0u;
            NextRepathTick = 0u;
            CurrentPathCornerIndex = -1;
            ControlIntent.Clear();
        }

        #endregion
    }
}
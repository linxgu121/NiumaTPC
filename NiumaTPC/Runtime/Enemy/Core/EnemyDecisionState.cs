namespace NiumaTPC.Enemy.Core
{
    /// <summary>
    /// 描述服务器AI当前正在执行的高层决策
    /// 不代表角色动画或动作状态
    /// </summary>
    public enum EnemyDecisionState : byte
    {
        /// <summary>
        /// 没有有效目标并保持待机
        /// </summary>
        Idle = 0,

        /// <summary>
        /// 向当前目标或最后已知位置移动
        /// </summary>
        Chase = 1,

        /// <summary>
        /// 处于攻击条件内并尝试发动攻击
        /// </summary>
        Attack = 2,

        /// <summary>
        /// 生命归零后停止感知和控制
        /// </summary>
        Dead = 3
    }
}
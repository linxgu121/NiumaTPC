namespace NiumaTPC.Character.Simulation
{
    /// <summary>
    /// 角色当前所处的确定性运动阶段。
    /// 客户端预测和服务器权威模拟必须得到相同结果。
    /// </summary>
    public enum CharacterMotionPhase : byte
    {
        /// <summary>
        /// 没有移动输入。
        /// </summary>
        Idle = 0,

        /// <summary>
        /// 刚开始移动，正在执行起步阶段。
        /// </summary>
        Starting = 1,

        /// <summary>
        /// 已经进入持续移动。
        /// </summary>
        Moving = 2,

        /// <summary>
        /// 松开输入后正在减速停止。
        /// </summary>
        Stopping = 3
    }
}
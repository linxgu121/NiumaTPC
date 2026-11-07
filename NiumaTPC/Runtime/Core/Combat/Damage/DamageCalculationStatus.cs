namespace NiumaTPC.Core.Combat
{
    /// <summary>
    /// 描述一次目标侧伤害计算的最终状态
    /// </summary>
    public enum DamageCalculationStatus : byte
    {
        /// <summary>
        /// 伤害计算成功且最终伤害大于零
        /// </summary>
        Applied = 0,

        /// <summary>
        /// 请求有效但最终伤害被修正为零
        /// </summary>
        Blocked = 1,

        /// <summary>
        /// 原始伤害请求不合法
        /// </summary>
        InvalidRequest = 2,

        /// <summary>
        /// 修正器产生NaN、Infinity或负数
        /// </summary>
        InvalidModifierResult = 3
    }
}

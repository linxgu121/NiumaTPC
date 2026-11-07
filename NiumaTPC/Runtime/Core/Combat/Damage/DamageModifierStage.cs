namespace NiumaTPC.Core.Combat
{
    /// <summary>
    /// 定义目标侧伤害修正器的执行阶段
    /// </summary>
    public enum DamageModifierStage : byte
    {
        /// <summary>
        /// 根据实际命中身体部位修正伤害
        /// </summary>
        HitSpot = 0,

        /// <summary>
        /// 处理防具、抗性、减伤和易伤状态
        /// </summary>
        Defense = 1,

        /// <summary>
        /// 处理最低伤害和全局倍率等最终规则
        /// </summary>
        Final = 2
    }
}

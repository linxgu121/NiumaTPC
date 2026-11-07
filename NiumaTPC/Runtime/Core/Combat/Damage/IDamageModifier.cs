namespace NiumaTPC.Core.Combat
{
    /// <summary>
    /// 由目标侧伤害规则实现的通用修正器接口
    /// </summary>
    public interface IDamageModifier
    {
        /// <summary>
        /// 当前修正器所属的计算阶段
        /// </summary>
        DamageModifierStage Stage { get; }

        /// <summary>
        /// 同阶段执行优先级，数值较小者先执行
        /// </summary>
        int Priority { get; }

        /// <summary>
        /// 修改当前伤害上下文
        /// 不能改写原始DamageRequest
        /// </summary>
        void Apply(ref DamageCalculationContext context);
    }
}

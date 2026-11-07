namespace NiumaTPC.Core.Combat
{
    /// <summary>
    /// 描述伤害管线的完整计算结果
    /// </summary>
    public readonly struct DamageCalculationResult
    {
        /// <summary>
        /// 本次计算的最终状态
        /// </summary>
        public readonly DamageCalculationStatus Status;

        /// <summary>
        /// 原始伤害请求
        /// </summary>
        public readonly DamageRequest Request;

        /// <summary>
        /// 进入目标侧管线前的基础伤害
        /// </summary>
        public readonly float BaseDamage;

        /// <summary>
        /// 所有目标侧修正完成后的最终伤害
        /// </summary>
        public readonly float FinalDamage;

        public bool IsApplied => Status == DamageCalculationStatus.Applied;

        public DamageCalculationResult(
            DamageCalculationStatus status,
            in DamageRequest request,
            float baseDamage,
            float finalDamage)
        {
            Status = status;
            Request = request;
            BaseDamage = baseDamage;
            FinalDamage = finalDamage;
        }
    }
}

namespace NiumaTPC.Core.Combat
{
    /// <summary>
    /// 保存单次伤害管线计算期间共享的数据
    /// 修正器只能修改CurrentDamage
    /// </summary>
    public struct DamageCalculationContext
    {
        /// <summary>
        /// 服务器提交的原始伤害请求
        /// </summary>
        public readonly DamageRequest Request;

        /// <summary>
        /// 进入目标侧管线前的基础伤害
        /// </summary>
        public readonly float BaseDamage;

        /// <summary>
        /// 当前阶段计算后的伤害
        /// </summary>
        public float CurrentDamage;

        public DamageCalculationContext(in DamageRequest request)
        {
            Request = request;
            BaseDamage = request.Amount;
            CurrentDamage = request.Amount;
        }
    }
}

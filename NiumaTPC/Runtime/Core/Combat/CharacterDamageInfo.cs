namespace NiumaTPC.Core.Combat
{
    /// <summary>
    /// 描述角色一次已经完成生命扣除的伤害结果
    /// </summary>
    public readonly struct CharacterDamageInfo
    {
        public readonly DamageRequest Request;
        public readonly float BaseDamage;
        public readonly float FinalDamage;
        public readonly float AppliedDamage;
        public readonly float HealthBefore;
        public readonly float HealthAfter;

        public bool IsLethal => HealthAfter <= 0f;

        public CharacterDamageInfo(
            in DamageRequest request,
            float baseDamage,
            float finalDamage,
            float appliedDamage,
            float healthBefore,
            float healthAfter)
        {
            Request = request;
            BaseDamage = baseDamage;
            FinalDamage = finalDamage;
            AppliedDamage = appliedDamage;
            HealthBefore = healthBefore;
            HealthAfter = healthAfter;
        }
    }
}

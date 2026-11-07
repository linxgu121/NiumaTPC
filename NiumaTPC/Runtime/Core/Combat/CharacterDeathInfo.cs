namespace NiumaTPC.Core.Combat
{
    /// <summary>
    /// 描述一次角色死亡及其致命伤害来源
    /// </summary>
    public readonly struct CharacterDeathInfo
    {
        /// <summary>
        /// 导致角色死亡的伤害请求
        /// </summary>
        public readonly DamageRequest LethalRequest;

        /// <summary>
        /// 致命伤害实际扣除的生命值
        /// </summary>
        public readonly float AppliedDamage;

        public CharacterDeathInfo(in DamageRequest lethalRequest, float appliedDamage)
        {
            LethalRequest = lethalRequest;
            AppliedDamage = appliedDamage;
        }
    }
}

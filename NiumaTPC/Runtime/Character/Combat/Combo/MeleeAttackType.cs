namespace NiumaTPC.Character.Combat
{
    /// <summary>
    /// 已成立近战攻击使用的攻击通道
    /// </summary>
    public enum MeleeAttackType : byte
    {
        /// <summary>
        /// 当前没有近战攻击
        /// </summary>
        None = 0,
        /// <summary>
        /// 主要近战攻击通道
        /// 玩家对应左键轻击，普通怪物对应主要攻击
        /// </summary>
        Light = 1,
        /// <summary>
        /// 次要近战攻击通道
        /// 玩家通常对应右键重击
        /// </summary>
        Heavy = 2
    }
}
namespace NiumaTPC.Item
{
    /// <summary>
    /// 一段弹道的碰撞查询结果
    /// 查询结果不等于子弹的生命周期状态。
    /// </summary>
    public enum AmmunitionCastResult : byte
    {
        /// <summary>
        /// 本段没有发现有效阻挡
        /// </summary>
        Clear = 0,
        /// <summary>
        /// 本段发现有效命中。
        /// </summary>
        Hit = 1,

        /// <summary>
        /// 命中缓冲区达到上限，无法确认结果是否完整
        /// 不能将这种情况当作没有碰撞
        /// </summary>
        Overflow = 2
    }
}
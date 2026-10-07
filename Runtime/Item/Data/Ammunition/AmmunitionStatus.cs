namespace NiumaTPC.Item
{
    /// <summary>
    /// 单颗子弹的生命周期状态
    /// 默认值为未启用，避免空结构体被当作有效子弹
    /// </summary>
    public enum AmmunitionStatus : byte
    {
        Inactive = 0,
        /// <summary> 
        ///  飞行中
        /// </summary>
        Flying = 1,
        /// <summary>
        /// 已命中
        /// </summary>
        Hit = 2,
        /// <summary>
        /// 子弹寿命到期
        /// </summary>
        Expired = 3,
        /// <summary>
        /// 主动取消或因无法安全继续模拟而结束
        /// </summary>
        Cancelled = 4
    }
}

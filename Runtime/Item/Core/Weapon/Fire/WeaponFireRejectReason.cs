namespace NiumaTPC.Item
{
    /// <summary>
    /// 开火请求被拒绝的原因
    /// 显式编号，后续用于网络消息时不要随意调整已有值
    /// </summary>
    public enum WeaponFireRejectReason : byte
    {
        None = 0,                   // 没有拒绝，发射成功
        InvalidRequestSequence = 1, // 请求编号无效
        StaleRequest = 2,           // 请求重复或已经落后
        Unavailable = 3,            // 角色或发射入口、驱动不可用
        Dead = 4,                   // 角色已经死亡
        ActionBlocked = 5,          // 当前动作限制开火
        NotAiming = 6,              // 当前没有瞄准
        EquipmentMismatch = 7,      // 手持实例或装备代次不匹配
        InvalidWeapon = 8,          // 武器类型或运行时状态无效
        EmptyMagazine = 9,          // 弹匣为空
        Cooldown = 10,              // 开火冷却尚未结束
        SpawnFailed = 11,           // 弹道驱动未能登记子弹
        InvalidAim = 12             // 瞄准起点或方向数值无效
    }
}
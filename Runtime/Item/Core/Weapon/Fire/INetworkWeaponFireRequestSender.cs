namespace NiumaTPC.Item
{
    /// <summary>
    /// 角色网络开火意图的本地提交接口
    /// ItemInstance 仅供本地检查，不会作为网络消息发送
    /// </summary>
    public interface INetworkWeaponFireRequestSender
    {
        // true 仅表示请求已提交，不代表服务器允许开火
        bool TrySubmitFire(ItemInstance weaponInstance, uint equipmentRevision);
    }
}
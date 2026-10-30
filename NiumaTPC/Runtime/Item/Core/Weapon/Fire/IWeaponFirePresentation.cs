using UnityEngine;

namespace NiumaTPC.Item
{
    /// <summary>
    /// 开火表现入口，可供本地预表现和远端确认表现共用
    /// 不发送请求、不创建逻辑子弹、不扣弹
    /// </summary>
    public interface IWeaponFirePresentation
    {
        /// <param name="expectedItem">本次表现对应的本地物品实例</param>
        /// <param name="applyRecoil">是否影响本地玩家视角</param>
        bool TryPlayFire(ItemInstance expectedItem, bool applyRecoil);

        /// <summary>
        /// 校验当前本地装备实例并取得视觉弹的枪口起点
        /// 服务器装备代次由网络武器驱动负责校验
        /// </summary>
        bool TryGetFirePresentationOrigin(
            ItemInstance expectedItem,
            out Vector3 origin);
    }
}

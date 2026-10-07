using System;

namespace NiumaTPC.Item
{
    /// <summary>
    /// 一件远程武器独有的运行时状态
    /// 跟随逻辑物品实例，不跟随武器模型的生成和回收
    /// </summary>
    public class RangedWeaponRuntimeState
    {
        /// <summary>
        /// 当前弹匣内剩余弹药，不包含背包中的备用弹药
        /// </summary>
        public int AmmoInMagazine { get; private set; }

        /// <summary>
        /// 下一次允许射击的会话时间，单位为秒
        /// 时间由外部驱动提供，不在这里读取 Unity Time
        /// </summary>
        public double NextAllowedFireTime { get; private set; }

        public RangedWeaponRuntimeState(int initialAmmo)
        {
            AmmoInMagazine = initialAmmo;

            // 新实例没有历史冷却，允许首次射击
            NextAllowedFireTime = 0d;
        }

        #region 开火状态

        /// <summary>
        /// 只检查状态，不扣弹，也不修改冷却
        /// </summary>
        public bool CanFire(double currentTime)
        {
            return AmmoInMagazine > 0 && currentTime >= NextAllowedFireTime;
        }

        /// <summary>
        /// 逻辑子弹登记成功后，由同一次同步发射流程提交
        /// 不允许由动画事件或模型池化回调调用
        /// </summary>
        public void CommitAcceptedShot(double currentTime, double fireInterval)
        {
            if (!CanFire(currentTime))
            {
                throw new InvalidOperationException("武器状态不允许提交射击，请检查重复提交或调用顺序");
            }

            AmmoInMagazine--;
            NextAllowedFireTime = currentTime + fireInterval;
        }

        #endregion

    }
}

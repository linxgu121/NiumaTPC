using Animancer;
using NiumaTPC.Character.Core.Animation;
using UnityEngine;

namespace NiumaTPC.Item
{
    [CreateAssetMenu(fileName = "New Ranged Weapon", menuName = "NiumaTPC/Items/Weapons/Ranged Weapon")]
    public class RangedWeaponSO : EquippableItemSO
    {
        [Header("--- 枪械独有配置 (Ranged Stats) ---")]
        [Tooltip("瞄准动画")]
        public ClipTransition AimAnim;
        public AnimPlayOptions AnimPlayOptions = AnimPlayOptions.UpperBodyDefault;

        [Tooltip("最大弹药量")]
        public int MaxAmmo = 30;

        [Tooltip("开火间隔 (秒)")]
        public float FireRate = 0.1f;

        [Tooltip("子弹发射初速度，单位 m/s，由武器提供")]
        public float ProjectileSpeed = 20f;

        [Tooltip("绑定这把武器使用的 AmmunitionDefinitionSO 提供弹道参数和表现资源")]
        public AmmunitionDefinitionSO Ammunition;

        // 如果你有专门的瞄准动画、换弹动画，统统配在这里
        // public ClipTransition AimIdleAnim; 
    }
}

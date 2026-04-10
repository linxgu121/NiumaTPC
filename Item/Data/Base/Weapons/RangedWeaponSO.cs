using System.Collections;
using System.Collections.Generic;
using Animancer;
using NiumaTPC.Item.Core.Animation;
using UnityEngine;

namespace NiumaTPC.Item.Data.Base.Weapon
{
    [CreateAssetMenu(fileName = "New Ranged Weapon", menuName = "NiumaTPC/Items/Weapons/Ranged Weapon")]
    public class RangedWeaponSO : EquippableItemSO
    {
        [Header("--- 枪械独有配置 (Ranged Stats) ---")]
        [Tooltip("瞄准动画")]
        public ClipTransition AimAnim;
        public AnimPlayOptions AnimPlayOptions=AnimPlayOptions.UpperBodyDefault;

        [Tooltip("最大弹药量")]
        public int MaxAmmo = 30;

        [Tooltip("开火间隔 (秒)")]
        public float FireRate = 0.1f;

        // 如果你有专门的瞄准动画、换弹动画，统统配在这里
        // public ClipTransition AimIdleAnim; 
    }
}

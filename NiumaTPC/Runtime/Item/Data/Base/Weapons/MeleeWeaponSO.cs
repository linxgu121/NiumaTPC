using NiumaTPC.Character.Combat;
using NiumaTPC.Character.Simulation;
using UnityEngine;

namespace NiumaTPC.Item
{
    /// <summary>
    /// 近战武器基类：定义近战武器的基础属性和动画配置。
    /// 继承自 EquippableItemSO，添加近战特定的动画和参数。
    /// </summary>
    [CreateAssetMenu(fileName = "NewMeleeWeaponSO", menuName = "NiumaTPC/Items/Weapons/Melee")]
    public class MeleeWeaponSO : EquippableItemSO
    {
        [Header("--- 拔出硬直 ---")]
        [Tooltip("拔出动画允许退出时间（秒）")]
        public float EquipEndTime = 0.5f;

        [Header("--- 近战连招 ---")]

        [Tooltip("左键轻击使用的连招配置")]
        public MeleeComboSO LightCombo;

        [Tooltip("右键重击使用的连招配置")]
        public MeleeComboSO HeavyCombo;

        [Header("--- 近战武器独有配置 ---")]
        [Tooltip("双手武器是否使用左手辅助握持IK，单手武器关闭")]
        public bool EnableIK;

        [Min(0f)]
        [Tooltip("从装备动画开始到启用左手辅助握持IK的等待时间")]
        public float EnableIKTime = 0.4f;

        [Header("--- 近战吸附 ---")]

        [Tooltip("是否允许该武器的近战攻击触发吸附")]
        public bool EnableAdsorption = true;

        [Min(0.01f)]
        [Tooltip("单次吸附允许角色移动的最大总距离，单位米")]
        public float AdsorptionMaximumTravelDistance = 2f;

        [Min(0.01f)]
        [Tooltip("吸附期间允许的最大水平速度，单位米每秒")]
        public float AdsorptionMaximumSpeed = 10f;

        [Min(0.01f)]
        [Tooltip("单次吸附允许持续的最长时间，运行时会转换成固定Tick数量")]
        public float AdsorptionDurationSeconds = 0.2f;

        [Min(0f)]
        [Tooltip("扣除攻击者与目标碰撞半径后额外保留的表面间隔")]
        public float AdsorptionStoppingSurfaceGap = 0.15f;

        [Min(0.01f)]
        [Tooltip("吸附期间每秒允许修正的最大朝向角度")]
        public float AdsorptionMaximumTurnSpeedDegreesPerSecond = 720f;

        /// <summary>
        /// 检查武器吸附配置并烘焙固定 Tick 使用的运动参数
        /// 禁用吸附或配置非法时不创建 Profile
        /// </summary>
        public bool TryCreateAdsorptionMotionProfile(
            float tickDeltaTime,
            out MeleeAdsorptionMotionProfile motionProfile)
        {
            motionProfile = default;

            if (!EnableAdsorption ||
                !IsPositiveFinite(tickDeltaTime) ||
                !IsPositiveFinite(AdsorptionMaximumTravelDistance) ||
                !IsPositiveFinite(AdsorptionMaximumSpeed) ||
                !IsPositiveFinite(AdsorptionDurationSeconds) ||
                !IsNonNegativeFinite(AdsorptionStoppingSurfaceGap) ||
                !IsPositiveFinite(AdsorptionMaximumTurnSpeedDegreesPerSecond))
            {
                return false;
            }

            int durationTickCount = Mathf.Max(1, Mathf.CeilToInt(AdsorptionDurationSeconds / tickDeltaTime));

            motionProfile = new MeleeAdsorptionMotionProfile(
                AdsorptionMaximumSpeed,
                (uint)durationTickCount,
                AdsorptionStoppingSurfaceGap,
                AdsorptionMaximumTurnSpeedDegreesPerSecond);

            return motionProfile.IsValid;
        }

        /// <summary>
        /// 根据攻击通道返回该武器对应的连招配置
        /// 未知攻击类型不提供连招
        /// </summary>
        public MeleeComboSO GetCombo(MeleeAttackType attackType)
        {
            return attackType switch
            {
                MeleeAttackType.Light => LightCombo,
                MeleeAttackType.Heavy => HeavyCombo,
                _ => null
            };
        }

        /// <summary>
        /// 检查数值是否为大于零的有限值
        /// </summary>
        private static bool IsPositiveFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;
        }

        /// <summary>
        /// 检查数值是否为非负有限值
        /// </summary>
        private static bool IsNonNegativeFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f;
        }
    }
}

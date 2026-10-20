using NiumaTPC.Character.Simulation;
using UnityEngine;

namespace NiumaTPC.Item
{
    /// <summary>
    /// 近战武器基类：定义近战武器的基础属性和动画配置。
    /// 继承自 EquippableItemSO，添加近战特定的动画和参数。
    /// </summary>
    public abstract class MeleeWeaponSO : EquippableItemSO
    {
        [Header("--- 拔出硬直 ---")]
        [Tooltip("拔出动画允许退出时间（秒）")]
        public float EquipEndTime = 0.5f;

        [Header("--- 近战武器独有配置 ---")]
        [Tooltip("攻击冷却时间 (秒)")]
        public float AttackCooldown = 0.5f;

        [Tooltip("是否启用 IK（近战通常不需要）")]
        public bool EnableIK = false;

        [Header("--- 近战吸附 ---")]

        [Tooltip("是否允许该武器的当前基础攻击触发近战吸附")]
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
                !IsPositiveFinite(
                    AdsorptionMaximumTurnSpeedDegreesPerSecond))
            {
                return false;
            }

            int durationTickCount = Mathf.Max(1, Mathf.CeilToInt(AdsorptionDurationSeconds / tickDeltaTime));

            motionProfile =
                new MeleeAdsorptionMotionProfile(
                    AdsorptionMaximumSpeed,
                    (uint)durationTickCount,
                    AdsorptionStoppingSurfaceGap,
                    AdsorptionMaximumTurnSpeedDegreesPerSecond);

            return motionProfile.IsValid;
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
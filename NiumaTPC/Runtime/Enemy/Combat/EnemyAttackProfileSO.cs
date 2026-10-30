using NiumaTPC.Character.Combat;
using NiumaTPC.Character.Simulation;
using UnityEngine;

namespace NiumaTPC.Enemy.Combat
{
    /// <summary>
    /// 定义普通怪物自身拥有的主要近战攻击
    /// 不代表可拾取、切换或持久化的装备
    /// </summary>
    [CreateAssetMenu(fileName = "EnemyAttackProfile", menuName = "NiumaTPC/Enemy/Enemy Attack Profile")]
    public sealed class EnemyAttackProfileSO : ScriptableObject
    {
        #region Identity(配置身份)

        [Tooltip("攻击配置的稳定非零编号，同一联机版本内不能与其他怪物攻击配置重复")]
        public uint AttackProfileId = 1u;

        #endregion

        #region Attack(主要攻击)

        [Header("主要攻击")]

        [Tooltip("普通怪物主要攻击使用的连招，第一版通常只配置一个招式")]
        public MeleeComboSO PrimaryCombo;

        [Min(0f)]
        [Tooltip("扣除攻击者和目标碰撞半径后，允许发动攻击的最大表面距离")]
        public float AttackStartSurfaceDistance = 0.5f;

        [Range(0f, 180f)]
        [Tooltip("目标相对怪物正前方允许发动攻击的最大夹角")]
        public float AttackMaximumAngleFromForward = 30f;

        #endregion

        #region Adsorption(近战吸附)

        [Header("近战吸附")]

        [Tooltip("主要攻击是否允许角色向目标进行吸附移动")]
        public bool EnableAdsorption = true;

        [Min(0.01f)]
        [Tooltip("单次吸附允许怪物移动的最大总距离")]
        public float AdsorptionMaximumTravelDistance = 1.5f;

        [Min(0.01f)]
        [Tooltip("吸附期间允许的最大水平速度")]
        public float AdsorptionMaximumSpeed = 8f;

        [Min(0.01f)]
        [Tooltip("单次吸附允许持续的最长时间，运行时转换成固定Tick数量")]
        public float AdsorptionDurationSeconds = 0.2f;

        [Min(0f)]
        [Tooltip("扣除双方碰撞半径后额外保留的表面间隔")]
        public float AdsorptionStoppingSurfaceGap = 0.15f;

        [Min(0.01f)]
        [Tooltip("吸附期间每秒允许修正的最大朝向角度")]
        public float AdsorptionMaximumTurnSpeedDegreesPerSecond = 720f;

        #endregion

        #region Public API(公共接口)

        /// <summary>
        /// 检查主要攻击与吸附配置并生成公共只读攻击定义
        /// 普通怪物第一版只支持Light主要攻击通道
        /// </summary>
        public bool TryCreateDefinition(
            MeleeAttackType attackType,
            float tickDeltaTime,
            out MeleeAttackDefinition definition)
        {
            definition = default;

            if (attackType != MeleeAttackType.Light ||
                AttackProfileId == 0u ||
                PrimaryCombo == null ||
                PrimaryCombo.StepCount <= 0)
            {
                return false;
            }

            MeleeAdsorptionMotionProfile motionProfile = default;

            if (EnableAdsorption &&
                !TryCreateAdsorptionMotionProfile(tickDeltaTime, out motionProfile))
            {
                return false;
            }

            definition = new MeleeAttackDefinition(
                attackType,
                PrimaryCombo,
                AttackProfileId,
                EnableAdsorption,
                AdsorptionMaximumTravelDistance,
                in motionProfile);

            return definition.IsValid;
        }

        #endregion

        #region Unity Lifecycle(Unity 生命周期)

        /// <summary>
        /// 在编辑器中限制距离、角度和吸附参数的合法范围
        /// </summary>
        private void OnValidate()
        {
            AttackStartSurfaceDistance = Mathf.Max(0f, AttackStartSurfaceDistance);
            AttackMaximumAngleFromForward = Mathf.Clamp(AttackMaximumAngleFromForward, 0f, 180f);
            AdsorptionMaximumTravelDistance = Mathf.Max(0.01f, AdsorptionMaximumTravelDistance);
            AdsorptionMaximumSpeed = Mathf.Max(0.01f, AdsorptionMaximumSpeed);
            AdsorptionDurationSeconds = Mathf.Max(0.01f, AdsorptionDurationSeconds);
            AdsorptionStoppingSurfaceGap = Mathf.Max(0f, AdsorptionStoppingSurfaceGap);
            AdsorptionMaximumTurnSpeedDegreesPerSecond =
                Mathf.Max(0.01f, AdsorptionMaximumTurnSpeedDegreesPerSecond);
        }

        #endregion

        #region Validation(数据校验)

        /// <summary>
        /// 检查吸附参数并转换成固定Tick运动配置
        /// 关闭吸附时不会调用该方法
        /// </summary>
        private bool TryCreateAdsorptionMotionProfile(
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

            uint durationTicks = (uint)Mathf.Max(1, Mathf.CeilToInt(AdsorptionDurationSeconds / tickDeltaTime));

            motionProfile = new MeleeAdsorptionMotionProfile(
                AdsorptionMaximumSpeed,
                durationTicks,
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

        #endregion
    }
}
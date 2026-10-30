using System;
using Animancer;
using NiumaTPC.Character.Core.Animation;
using UnityEngine;

namespace NiumaTPC.Character.Combat
{
    /// <summary>
    /// 一套近战连招中的单段攻击配置
    /// </summary>
    [Serializable]
    public sealed class MeleeComboStep
    {
        #region Inspector

        [Tooltip("招式名称")]
        public string DisplayName = "攻击";

        [Tooltip("该段攻击使用的动画")]
        public ClipTransition AttackAnim;

        [Tooltip("该段攻击动画使用的层级、淡入、速度和起始时间")]
        public AnimPlayOptions AttackAnimPlayOptions = new AnimPlayOptions
        {
            Layer = 1,
            FadeDuration = 0.1f,
            Speed = 1f,
            NormalizedTime = 0f,
            ForcePhaseSync = false
        };

        [Header("攻击判定")]

        [Min(0.01f)]
        [Tooltip("该段攻击命中目标时提交的基础伤害，部位倍率由受击目标处理")]
        public float Damage = 10f;

        [Min(0f)]
        [Tooltip("从攻击成立开始到开启服务器命中判定的时间")]
        public float HitWindowStartSeconds = 0.15f;

        [Min(0.01f)]
        [Tooltip("从攻击成立开始到关闭服务器命中判定的时间")]
        public float HitWindowEndSeconds = 0.35f;

        [Tooltip("攻击盒中心相对角色根节点的位置 使用角色局部坐标")]
        public Vector3 HitBoxLocalCenter = new Vector3(0f, 1f, 1f);

        [Tooltip("攻击盒三个方向的半尺寸 必须全部大于零")]
        public Vector3 HitBoxHalfExtents = new Vector3(0.8f, 0.8f, 1f);

        [Header("连招衔接")]

        [Min(0f)]
        [Tooltip("从本段攻击成立开始 最早允许下一段攻击成立的时间")]
        public float NextAttackWindowStartSeconds = 0.25f;

        [Min(0.01f)]
        [Tooltip("从本段攻击成立开始 超过该时间后连招会重置")]
        public float NextAttackWindowEndSeconds = 0.8f;

        #endregion

        #region Public API

        /// <summary>
        /// 检查连招时间窗口并转换成固定 Tick 使用的确定性数据
        /// 转换后至少保留一个冷却 Tick 和一个有效衔接 Tick
        /// </summary>
        public bool TryCreateTiming(
            float tickDeltaTime,
            out uint nextAttackWindowStartTicks,
            out uint nextAttackWindowEndTicks)
        {
            nextAttackWindowStartTicks = 0u;
            nextAttackWindowEndTicks = 0u;

            if (!IsPositiveFinite(tickDeltaTime) ||
                !IsNonNegativeFinite(NextAttackWindowStartSeconds) ||
                !IsPositiveFinite(NextAttackWindowEndSeconds) ||
                NextAttackWindowEndSeconds <= NextAttackWindowStartSeconds)
            {
                return false;
            }

            int startTickCount = Mathf.Max(
                1,
                Mathf.CeilToInt(
                    NextAttackWindowStartSeconds /
                    tickDeltaTime));

            int endTickCount = Mathf.Max(
                startTickCount + 1,
                Mathf.CeilToInt(
                    NextAttackWindowEndSeconds /
                    tickDeltaTime));

            nextAttackWindowStartTicks = (uint)startTickCount;

            nextAttackWindowEndTicks = (uint)endTickCount;

            return true;
        }

        /// <summary>
        /// 检查攻击判定窗口与盒体配置并转换成固定 Tick 数据
        /// 开始边界向下取整 结束边界向上取整并至少保留一个有效 Tick
        /// </summary>
        public bool TryCreateHitWindow(
            float tickDeltaTime,
            out uint hitWindowStartTicks,
            out uint hitWindowEndTicks)
        {
            hitWindowStartTicks = 0u;
            hitWindowEndTicks = 0u;

            if (!IsPositiveFinite(tickDeltaTime) ||
                !IsPositiveFinite(Damage) ||
                !IsNonNegativeFinite(HitWindowStartSeconds) ||
                !IsPositiveFinite(HitWindowEndSeconds) ||
                HitWindowEndSeconds <= HitWindowStartSeconds ||
                !IsFinite(HitBoxLocalCenter) ||
                !IsPositiveFinite(HitBoxHalfExtents))
            {
                return false;
            }

            int startTickCount = Mathf.Max(
                0,
                Mathf.FloorToInt(HitWindowStartSeconds / tickDeltaTime));

            int endTickCount = Mathf.Max(
                startTickCount + 1,
                Mathf.CeilToInt(HitWindowEndSeconds / tickDeltaTime));

            hitWindowStartTicks = (uint)startTickCount;

            hitWindowEndTicks = (uint)endTickCount;

            return true;
        }

        #endregion

        #region Validation

        // 检查向量三个分量是否都是有限值
        private static bool IsFinite(Vector3 value)
        {
            return !float.IsNaN(value.x) &&
                   !float.IsInfinity(value.x) &&
                   !float.IsNaN(value.y) &&
                   !float.IsInfinity(value.y) &&
                   !float.IsNaN(value.z) &&
                   !float.IsInfinity(value.z);
        }

        // 检查向量三个分量是否都是大于零的有限值
        private static bool IsPositiveFinite(Vector3 value)
        {
            return IsPositiveFinite(value.x) &&
                   IsPositiveFinite(value.y) &&
                   IsPositiveFinite(value.z);
        }

        // 检查数值是否为大于零的有限值
        private static bool IsPositiveFinite(float value)
        {
            return !float.IsNaN(value) &&
                   !float.IsInfinity(value) &&
                   value > 0f;
        }

        // 检查数值是否为非负有限值
        private static bool IsNonNegativeFinite(float value)
        {
            return !float.IsNaN(value) &&
                   !float.IsInfinity(value) &&
                   value >= 0f;
        }


        #endregion
    }
}
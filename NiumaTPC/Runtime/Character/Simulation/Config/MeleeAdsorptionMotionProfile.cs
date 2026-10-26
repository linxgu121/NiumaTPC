namespace NiumaTPC.Character.Simulation
{
    /// <summary>
    /// 固定 Tick 吸附模拟使用的运动参数
    /// 由具体近战攻击配置生成，不接受客户端自行指定
    /// </summary>
    public readonly struct MeleeAdsorptionMotionProfile
    {
        /// <summary>
        /// 吸附期间允许的最大水平速度，单位 m/s
        /// </summary>
        public readonly float MaximumSpeed;

        /// <summary>
        /// 吸附最多持续的固定 Tick 数
        /// </summary>
        public readonly uint DurationTicks;

        /// <summary>
        /// 扣除攻击者和目标碰撞半径后额外保留的水平间隔
        /// 可用于表达不同武器或攻击动作的有效接近距离
        /// </summary>
        public readonly float StoppingSurfaceGap;

        /// <summary>
        /// 每秒允许修正的最大朝向角度
        /// </summary>
        public readonly float MaximumTurnSpeedDegreesPerSecond;

        /// <summary>
        /// 检查运动参数能否安全用于预测与服务器权威模拟
        /// </summary>
        public bool IsValid =>
            IsPositiveFinite(MaximumSpeed) &&
            DurationTicks > 0u &&
            IsNonNegativeFinite(StoppingSurfaceGap) &&
            IsPositiveFinite(
                MaximumTurnSpeedDegreesPerSecond);

        public MeleeAdsorptionMotionProfile(
            float maximumSpeed,
            uint durationTicks,
            float stoppingSurfaceGap,
            float maximumTurnSpeedDegreesPerSecond)
        {
            MaximumSpeed = maximumSpeed;
            DurationTicks = durationTicks;
            StoppingSurfaceGap = stoppingSurfaceGap;
            MaximumTurnSpeedDegreesPerSecond =
                maximumTurnSpeedDegreesPerSecond;
        }

        /// <summary>
        /// 检查数值是否为大于零的有限值
        /// </summary>
        private static bool IsPositiveFinite(float value)
        {
            return
                !float.IsNaN(value) &&
                !float.IsInfinity(value) &&
                value > 0f;
        }

        /// <summary>
        /// 检查数值是否为非负有限值
        /// </summary>
        private static bool IsNonNegativeFinite(float value)
        {
            return
                !float.IsNaN(value) &&
                !float.IsInfinity(value) &&
                value >= 0f;
        }
    }
}
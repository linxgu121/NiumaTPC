namespace NiumaTPC.Character.Simulation
{
    /// <summary>
    /// 已转换为固定 Tick 单位的滑铲权威运动配置。
    /// 不持有 ScriptableObject 或动画资源，可供服务器和预测客户端共同使用。
    /// </summary>
    public readonly struct CharacterSlideMotionProfile
    {
        #region Motion Data(运动数据)

        public readonly float MinimumStartSpeed;
        public readonly float StartSpeedMultiplier;
        public readonly float MaximumStartSpeed;

        /// <summary>
        /// 每个固定 Tick 减少的水平速度。
        /// </summary>
        public readonly float DecelerationPerTick;

        public readonly float ExitSpeed;

        public readonly uint MinimumDurationTicks;
        public readonly uint MaximumDurationTicks;

        public readonly bool ApplyGravity;

        #endregion

        #region Properties(属性)

        public bool IsValid =>
            IsFinite(MinimumStartSpeed) &&
            IsFinite(StartSpeedMultiplier) &&
            IsFinite(MaximumStartSpeed) &&
            IsFinite(DecelerationPerTick) &&
            IsFinite(ExitSpeed) &&
            MinimumStartSpeed > ExitSpeed &&
            StartSpeedMultiplier > 0f &&
            MaximumStartSpeed >= MinimumStartSpeed &&
            DecelerationPerTick > 0f &&
            ExitSpeed >= 0f &&
            MinimumDurationTicks > 0u &&
            MaximumDurationTicks >= MinimumDurationTicks;

        #endregion

        #region Constructor(构造器)

        public CharacterSlideMotionProfile(
            float minimumStartSpeed,
            float startSpeedMultiplier,
            float maximumStartSpeed,
            float decelerationPerTick,
            float exitSpeed,
            uint minimumDurationTicks,
            uint maximumDurationTicks,
            bool applyGravity)
        {
            MinimumStartSpeed = minimumStartSpeed;
            StartSpeedMultiplier = startSpeedMultiplier;
            MaximumStartSpeed = maximumStartSpeed;
            DecelerationPerTick = decelerationPerTick;
            ExitSpeed = exitSpeed;
            MinimumDurationTicks = minimumDurationTicks;
            MaximumDurationTicks = maximumDurationTicks;
            ApplyGravity = applyGravity;
        }

        #endregion

        #region Helpers(辅助工具)

        /// <summary>
        /// 校验浮点数是否为合法有限数值
        /// </summary>
        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        #endregion
    }
}

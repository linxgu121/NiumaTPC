using UnityEngine;

namespace NiumaTPC.Character.Simulation
{
    /// <summary>
    /// 根据输入方向和当前模拟状态，推进角色的运动阶段。
    ///
    /// 本类只修改确定性模拟状态，不读取输入设备、动画或网络对象。
    /// 因此客户端预测、服务器模拟和回滚重演都能复用。
    /// </summary>
    public static class CharacterMotionPhaseResolver
    {
        #region Constants

        /// <summary>
        /// 两个单位方向的点积小于 0，代表夹角超过 90 度。
        /// 此时认为玩家进行了明显反向操作。
        /// </summary>
        private const float ReverseDirectionDotThreshold = 0f;

        /// <summary>
        /// 速度低于该值时，可以认为停止过程已经完成。
        /// </summary>
        private const float StopSpeedEpsilon = 0.05f;

        private const float DirectionEpsilonSquared = 0.0001f;

        #endregion

        #region Public API

        public static void Update(
            ref CharacterSimulationState state,
            bool hasMovement,
            in Vector3 requestedDirection,
            in CharacterSimulationConfig config,
            float tickDeltaTime)
        {
            uint stoppingDurationTicks = CalculatePhaseDurationTicks(
                config.MoveSpeedSmoothTime,
                tickDeltaTime);

            if (!hasMovement)
            {
                UpdateWithoutMovement(
                    ref state,
                    stoppingDurationTicks);

                return;
            }

            UpdateWithMovement(
                ref state,
                in requestedDirection,
                in config,
                tickDeltaTime);
        }

        #endregion

        #region Movement Phase(移动阶段)

        private static void UpdateWithMovement(
            ref CharacterSimulationState state,
            in Vector3 requestedDirection,
            in CharacterSimulationConfig config,
            float tickDeltaTime)
        {
            bool hadPreviousDirection =
                state.LastMoveDirection.sqrMagnitude >
                DirectionEpsilonSquared;

            bool isReversing =
                hadPreviousDirection &&
                Vector3.Dot(
                    state.LastMoveDirection,
                    requestedDirection) <
                ReverseDirectionDotThreshold;

            switch (state.MotionPhase)
            {
                case CharacterMotionPhase.Idle:
                    BeginStarting( ref state, in requestedDirection, resetSpeed: true);
                    break;

                case CharacterMotionPhase.Stopping:
                    // 如果停止过程中再次按下同方向，可以继承尚未完全归零的速度。
                    // 如果改为反方向，则必须清掉旧方向积累的速度。
                    BeginStarting(ref state, in requestedDirection, resetSpeed: isReversing);
                    break;

                case CharacterMotionPhase.Starting:
                    if (isReversing)
                    {
                        BeginStarting(ref state, in requestedDirection, resetSpeed: true);
                    }
                    else if (state.LocomotionState != state.StartLocomotionState)
                    {
                        // 与旧 PlayerMoveStartState 保持一致：
                        // 起步时改变 Walk/Jog/Sprint，直接进入输入驱动阶段，
                        // 不在半途中切换另一条起步曲线。
                        EnterPhase(ref state, CharacterMotionPhase.Moving);
                        state.RotationSmoothVelocity = 0f;
                    }
                    else
                    {
                        AdvancePhaseTick(ref state);

                        uint startingDurationTicks =
                            GetStartingDurationTicks(
                                in state,
                                in config,
                                tickDeltaTime);

                        if (state.MotionPhaseTick >= startingDurationTicks)
                        {
                            EnterPhase( ref state, CharacterMotionPhase.Moving);
                        }
                    }

                    break;

                case CharacterMotionPhase.Moving:
                    if (isReversing)
                    {
                        BeginStarting( ref state, in requestedDirection, resetSpeed: true);
                    }
                    else
                    {
                        AdvancePhaseTick(ref state);
                    }

                    break;
            }

            // 必须在反向判断完成后再覆盖上一方向。
            state.LastMoveDirection = requestedDirection;
        }

        private static void UpdateWithoutMovement(
            ref CharacterSimulationState state,
            uint stoppingDurationTicks)
        {
            switch (state.MotionPhase)
            {
                case CharacterMotionPhase.Idle:
                    ResetIdleMotion(ref state);
                    break;

                case CharacterMotionPhase.Starting:
                case CharacterMotionPhase.Moving:
                    EnterPhase(
                        ref state,
                        CharacterMotionPhase.Stopping);

                    state.RotationSmoothVelocity = 0f;
                    break;

                case CharacterMotionPhase.Stopping:
                    AdvancePhaseTick(ref state);

                    bool durationFinished =
                        state.MotionPhaseTick >= stoppingDurationTicks;

                    bool speedFinished =
                        state.SmoothSpeed <= StopSpeedEpsilon;

                    if (durationFinished || speedFinished)
                    {
                        EnterPhase(
                            ref state,
                            CharacterMotionPhase.Idle);

                        ResetIdleMotion(ref state);
                    }

                    break;
            }
        }

        #endregion

        #region Phase Transition(阶段切换)

        private static void BeginStarting(ref CharacterSimulationState state, in Vector3 requestedDirection,bool resetSpeed)
        {
            EnterPhase(ref state, CharacterMotionPhase.Starting);

            //只在进入 Starting 的瞬间选择一次方向。
            // 后续轻微摇杆波动不会切换起步曲线。
            state.StartDirection = CharacterStartDirectionResolver.Resolve(state.Yaw, in requestedDirection);
            state.StartLocomotionState = state.LocomotionState;


            if (resetSpeed)
            {
                // 旧方向的速度不能直接套用到新方向。
                state.SmoothSpeed = 0f;
                state.SpeedSmoothVelocity = 0f;
            }

            // 新一轮起步重新计算朝向平滑。
            state.RotationSmoothVelocity = 0f;
        }

        private static void EnterPhase(
            ref CharacterSimulationState state,
            CharacterMotionPhase phase)
        {
            state.MotionPhase = phase;
            state.MotionPhaseTick = 0u;
        }

        private static void AdvancePhaseTick(
            ref CharacterSimulationState state)
        {
            // 使用饱和递增，避免极长运行后发生 uint 回绕。
            if (state.MotionPhaseTick < uint.MaxValue)
            {
                state.MotionPhaseTick++;
            }
        }

        private static void ResetIdleMotion(
            ref CharacterSimulationState state)
        {
            state.SmoothSpeed = 0f;
            state.SpeedSmoothVelocity = 0f;
            state.RotationSmoothVelocity = 0f;
            state.LastMoveDirection = Vector3.zero;
            state.StartDirection = CharacterStartDirection.Forward;
            state.StartLocomotionState =
                NiumaTPC.Character.Motion.MotionEnums.LocomotionState.Idle;
        }

        #endregion

        #region Helpers(通用工具辅助函数集合)

        private static uint CalculatePhaseDurationTicks(
            float durationSeconds,
            float tickDeltaTime)
        {
            if (durationSeconds <= 0f)
            {
                return 1u;
            }

            int ticks = Mathf.Max(
                1,
                Mathf.CeilToInt(
                    durationSeconds / tickDeltaTime));

            return (uint)ticks;
        }

        private static uint GetStartingDurationTicks(
            in CharacterSimulationState state,
            in CharacterSimulationConfig config,
            float tickDeltaTime)
        {
            if (config.TryGetStartMotionProfile(
                    state.StartLocomotionState,
                    state.StartDirection,
                    out CharacterStartMotionProfile profile))
            {
                return profile.DurationTicks > 0u
                    ? profile.DurationTicks
                    : 1u;
            }

            // 某个方向没有有效曲线时仍允许移动，
            // 使用通用平滑时间作为安全降级。
            return CalculatePhaseDurationTicks(
                config.MoveSpeedSmoothTime,
                tickDeltaTime);
        }

        #endregion
    }
}

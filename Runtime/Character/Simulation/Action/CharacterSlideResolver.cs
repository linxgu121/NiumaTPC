using NiumaTPC.Character.Motion.MotionEnums;
using UnityEngine;

namespace NiumaTPC.Character.Simulation
{
    /// <summary>
    /// 负责滑铲的启动、跳跃打断判定和状态清理。
    /// 不计算位移，也不直接操作 CharacterController
    /// </summary>
    public static class CharacterSlideResolver
    {
        private const float DirectionSqrEpsilon = 0.0001f;

        #region Start(滑铲开始)

        /// <summary>
        /// 根据上一个已确认模拟状态和当前 Tick 输入尝试启动滑铲。
        /// </summary>
        public static bool TryStartSlide(
            ref CharacterSimulationState state,
            in CharacterInputCommand command,
            in CharacterSimulationConfig config)
        {
            if (!CanTryStartSlide(
                    in state,
                    in command))
            {
                return false;
            }

            if (!config.TryGetSlideMotionProfile(
                    out CharacterSlideMotionProfile profile))
            {
                return false;
            }

            float currentSpeed =
                Mathf.Max(0f, state.SmoothSpeed);

            if (currentSpeed < profile.MinimumStartSpeed)
            {
                return false;
            }

            Vector3 lockedDirection =
                state.LastMoveDirection;

            lockedDirection.y = 0f;

            // 已经处于 Sprint 却没有有效方向，说明状态数据不完整。
            // 这里拒绝启动，避免客户端和服务器采用不同兜底方向。
            if (lockedDirection.sqrMagnitude <
                DirectionSqrEpsilon)
            {
                return false;
            }

            lockedDirection.Normalize();

            BeginSlide(
                ref state,
                lockedDirection,
                currentSpeed,
                in profile);

            return true;
        }

        /// <summary>
        /// 滑铲启动条件预检查
        /// 明确优先级Jump > Roll > Dodge > Slide
        /// </summary>
        private static bool CanTryStartSlide(
            in CharacterSimulationState state,
            in CharacterInputCommand command)
        {
            bool hasHigherPriorityInput =
                command.HasButton(CharacterInputButtons.Jump) ||
                command.HasButton(CharacterInputButtons.Roll) ||
                command.HasButton(CharacterInputButtons.Dodge);

            if (hasHigherPriorityInput)
            {
                return false;
            }

            return
                command.HasButton(CharacterInputButtons.Slide) &&
                state.ActionType == CharacterActionType.None &&
                state.VaultType == VaultType.None &&
                state.IsGrounded &&
                state.LocomotionState == LocomotionState.Sprint;
        }

        /// <summary>
        /// 改写模拟状态，正式开启滑铲
        /// </summary>
        private static void BeginSlide(
            ref CharacterSimulationState state,
            Vector3 lockedDirection,
            float currentSpeed,
            in CharacterSlideMotionProfile profile)
        {
            float initialSpeed = Mathf.Clamp(
                currentSpeed * profile.StartSpeedMultiplier,
                profile.MinimumStartSpeed,
                profile.MaximumStartSpeed);

            state.ActionType = CharacterActionType.Slide;

            // ActionTick 表示已经完整执行的滑铲 Tick 数。
            state.ActionTick = 0u;

            /*
             * Slide 使用连续世界方向 LastMoveDirection，
             * 不使用 Roll/Dodge 的离散八方向。
             */
            state.ActionDirection = CharacterActionDirection.Forward;

            state.LastMoveDirection = lockedDirection;

            state.SlideSpeed = initialSpeed;

            state.PendingSlideJump = false;

            // SmoothSpeed 同步保存当前实际水平速度，
            // 方便结束或跳跃打断时继承速度。
            state.SmoothSpeed = initialSpeed;

            state.SpeedSmoothVelocity = 0f;
            state.RotationSmoothVelocity = 0f;

            // 清除尚未完成的起步或停止阶段，
            // 避免滑铲结束后继续播放旧运动曲线。
            state.MotionPhase = CharacterMotionPhase.Idle;

            state.MotionPhaseTick = 0u;
        }

        #endregion

        #region Jump Interrupt(跳跃打断)

        /// <summary>
        /// 记录本 Tick 的跳跃输入，并判断是否已经允许跳跃打断。
        /// 提前输入会保存在 PendingSlideJump 中。
        /// </summary>
        public static bool ShouldInterruptWithJump(
            ref CharacterSimulationState state,
            in CharacterInputCommand command,
            in CharacterSlideMotionProfile profile)
        {
            if (state.ActionType != CharacterActionType.Slide || !profile.IsValid)
            {
                return false;
            }

            if (command.HasButton(CharacterInputButtons.Jump))
            {
                state.PendingSlideJump = true;
            }

            return state.PendingSlideJump && state.ActionTick >= profile.MinimumDurationTicks;
        }

        #endregion

        #region Finish(结束滑铲)

        /// <summary>
        /// 结束滑铲并清除所有滑铲专用状态。
        /// 返回结束前是否存在缓存跳跃。
        /// </summary>
        /// <param name="preserveHorizontalSpeed">
        /// true：保留剩余滑铲速度，供跳跃或离开边缘时继承。
        /// false：清空速度，用于正面撞墙等情况。
        /// </param>
        public static bool FinishSlide(
            ref CharacterSimulationState state,
            bool preserveHorizontalSpeed)
        {
            bool hadPendingJump = state.PendingSlideJump;

            float remainingSpeed = Mathf.Max(0f, state.SlideSpeed);

            state.ActionType = CharacterActionType.None;

            state.ActionTick = 0u;

            state.ActionDirection = CharacterActionDirection.Forward;

            state.SlideSpeed = 0f;
            state.PendingSlideJump = false;

            state.SmoothSpeed =
                preserveHorizontalSpeed
                    ? remainingSpeed
                    : 0f;

            state.SpeedSmoothVelocity = 0f;
            state.RotationSmoothVelocity = 0f;

            /*
             * 保留速度时直接回到输入驱动阶段，
             * 避免跳跃打断后再次进入 Starting，
             * 导致继承的滑铲速度被起步曲线覆盖。
             */
            state.MotionPhase = preserveHorizontalSpeed ? CharacterMotionPhase.Moving : CharacterMotionPhase.Idle;

            state.MotionPhaseTick = 0u;

            if (!preserveHorizontalSpeed)
            {
                state.LastMoveDirection = Vector3.zero;
            }


            return hadPendingJump;
        }

        #endregion


    }
}

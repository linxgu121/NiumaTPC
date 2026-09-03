using System;
using UnityEngine;

namespace NiumaTPC.Character.Simulation
{
    /// <summary>
    /// 按固定 Tick 推进滑铲位移和速度衰减。
    /// 不直接移动 CharacterController，也不处理动画。
    /// </summary>
    public static class CharacterSlideMovementSimulator
    {
        private const float DirectionSqrEpsilon = 0.0001f;
        private const float SpeedEpsilon = 0.0001f;

        #region Public API

        public static bool TrySimulate(
            ref CharacterSimulationState state,
            in CharacterInputCommand command,
            in CharacterSimulationConfig config,
            float tickDeltaTime,
            out Vector3 horizontalDisplacement,
            out bool applyGravity,
            out bool forceJumpInput)
        {
            ValidateTickDeltaTime(tickDeltaTime);

            horizontalDisplacement = Vector3.zero;
            applyGravity = true;
            forceJumpInput = false;

            if (state.ActionType != CharacterActionType.Slide)
            {
                if (!CharacterSlideResolver.TryStartSlide(
                        ref state,
                        in command,
                        in config))
                {
                    return false;
                }
            }

            if (!config.TryGetSlideMotionProfile(out CharacterSlideMotionProfile profile))
            {
                CharacterSlideResolver.FinishSlide(
                    ref state,
                    preserveHorizontalSpeed: false);

                return false;
            }

            applyGravity = profile.ApplyGravity;

            /*
             * IsGrounded 是上一个 Tick 移动完成后的真实结果。
             * 一旦已经离地，Slide 不能继续接管水平移动。
             */
            if (!state.IsGrounded)
            {
                CharacterSlideResolver.FinishSlide(
                    ref state,
                    preserveHorizontalSpeed: true);

                return false;
            }

            Vector3 slideDirection = state.LastMoveDirection;

            slideDirection.y = 0f;

            if (slideDirection.sqrMagnitude < DirectionSqrEpsilon)
            {
                CharacterSlideResolver.FinishSlide(
                    ref state,
                    preserveHorizontalSpeed: false);

                return false;
            }

            slideDirection.Normalize();
            state.LastMoveDirection = slideDirection;

            /*
             * ActionTick 表示已经完成的滑铲 Tick 数。
             * 达到最短时间后，当前输入或之前缓存的输入
             * 都可以在本 Tick 打断滑铲。
             */
            bool shouldJump = CharacterSlideResolver.ShouldInterruptWithJump(
                        ref state,
                        in command,
                        in profile);

            if (shouldJump)
            {
                horizontalDisplacement =
                    slideDirection *
                    state.SlideSpeed *
                    tickDeltaTime;

                CharacterSlideResolver.FinishSlide(
                    ref state,
                    preserveHorizontalSpeed: true);

                forceJumpInput = true;
                return true;
            }

            /*
             * 使用配置中已经烘焙好的每 Tick 减速度。
             * 不读取 Time.deltaTime，保证预测和重演一致。
             */
            state.SlideSpeed = Mathf.MoveTowards(
                state.SlideSpeed,
                profile.ExitSpeed,
                profile.DecelerationPerTick);

            state.SmoothSpeed = state.SlideSpeed;
            state.SpeedSmoothVelocity = 0f;
            state.RotationSmoothVelocity = 0f;

            horizontalDisplacement = slideDirection * state.SlideSpeed * tickDeltaTime;

            if (state.ActionTick < uint.MaxValue)
            {
                state.ActionTick++;
            }

            bool speedFinished =
                state.SlideSpeed <=
                profile.ExitSpeed + SpeedEpsilon;

            bool durationFinished =
                state.ActionTick >=
                profile.MaximumDurationTicks;

            if (speedFinished || durationFinished)
            {
                bool hadPendingJump =
                    CharacterSlideResolver.FinishSlide(
                        ref state,
                        preserveHorizontalSpeed: true);

                /*
                 * 正常结束条件先于缓存跳跃触发时，
                 * 只要仍然接地，就在同一个 Tick 起跳。
                 */
                forceJumpInput =
                    hadPendingJump &&
                    state.IsGrounded;
            }

            return true;
        }

        #endregion

        #region Validation

        private static void ValidateTickDeltaTime(
            float tickDeltaTime)
        {
            bool isInvalid =
                tickDeltaTime <= 0f ||
                float.IsNaN(tickDeltaTime) ||
                float.IsInfinity(tickDeltaTime);

            if (isInvalid)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(tickDeltaTime),
                    tickDeltaTime,
                    "模拟 Tick 时长必须是有限的正数。");
            }
        }

        #endregion
    }
}

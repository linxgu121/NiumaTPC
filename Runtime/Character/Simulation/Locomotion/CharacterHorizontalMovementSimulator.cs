using System;
using UnityEngine;

namespace NiumaTPC.Character.Simulation
{
    /// <summary>
    /// 计算单个模拟 Tick 的水平位移。
    /// 不直接操作 Transform 或 CharacterController。
    /// </summary>
    public static class CharacterHorizontalMovementSimulator
    {
        #region Public API
        public static Vector3 Simulate(
            ref CharacterSimulationState state,
            in CharacterInputCommand command,
            in CharacterSimulationConfig config,
            bool canSprint,
            float tickDeltaTime
        )
        {
            ValidateTickDeltaTime(tickDeltaTime);

            //先计算本 Tick 的速度档位。
            // 没有输入时为 Idle，停止阶段会以此作为减速目标。
            state.LocomotionState = CharacterLocomotionResolver.Resolve(in command, canSprint);

            bool hasMovement = CharacterMovementDirectionResolver.TryResolve(in command, out Vector3 worldDirection, out float targetYaw);

            CharacterMotionPhaseResolver.Update(ref state, hasMovement, in worldDirection, in config, tickDeltaTime);

            switch (state.MotionPhase)
            {
                case CharacterMotionPhase.Idle:
                    return Vector3.zero;

                case CharacterMotionPhase.Stopping:
                    //LocomotionState 此时是 Idle
                    //因此 UpdateSpeed 会把目标速度设置为 0
                    UpdateSpeed(ref state, in config, tickDeltaTime);

                    return state.LastMoveDirection * state.SmoothSpeed * tickDeltaTime;

                case CharacterMotionPhase.Starting:
                    if (TrySimulateStartMotion(
                            ref state,
                            in config,
                            tickDeltaTime,
                            out Vector3 startDisplacement))
                    {
                        return startDisplacement;
                    }

                    // 某个起步资源无效时安全降级为普通输入驱动。
                    UpdateYaw(ref state, targetYaw, in config, tickDeltaTime);
                    UpdateSpeed(ref state, in config, tickDeltaTime);

                    return worldDirection * state.SmoothSpeed * tickDeltaTime;

                case CharacterMotionPhase.Moving:
                    UpdateYaw(ref state, targetYaw, in config, tickDeltaTime);

                    UpdateSpeed(ref state, in config, tickDeltaTime);
                    
                    return worldDirection * state.SmoothSpeed * tickDeltaTime;

                default:
                    return Vector3.zero;

            }

        }

        #endregion

        #region Simulation

        private static bool TrySimulateStartMotion(
            ref CharacterSimulationState state,
            in CharacterSimulationConfig config,
            float tickDeltaTime,
            out Vector3 displacement)
        {
            if (!config.TryGetStartMotionProfile(
                    state.StartLocomotionState,
                    state.StartDirection,
                    out CharacterStartMotionProfile profile))
            {
                displacement = Vector3.zero;
                return false;
            }

            uint phaseTick = state.MotionPhaseTick;

            // 旋转曲线保存的是累计角度，模拟时只应用本 Tick 的差值。
            // Tick 0 用自身作为上一值，保持旧 MotionDriver 首帧不突转的行为。
            float currentAccumulatedRotation =
                profile.GetAccumulatedRotation(phaseTick);

            float previousAccumulatedRotation =
                phaseTick == 0u
                    ? currentAccumulatedRotation
                    : profile.GetAccumulatedRotation(phaseTick - 1u);

            float deltaYaw =
                currentAccumulatedRotation - previousAccumulatedRotation;

            state.Yaw = Mathf.Repeat(state.Yaw + deltaYaw, 360f);
            state.RotationSmoothVelocity = 0f;

            float speed = profile.GetSpeed(phaseTick);

            state.SmoothSpeed = speed;
            state.SpeedSmoothVelocity = 0f;

            Vector3 localDirection =
                profile.LocalDirection.sqrMagnitude > 0.0001f
                    ? profile.LocalDirection
                    : Vector3.forward;

            Vector3 worldDirection =
                Quaternion.Euler(0f, state.Yaw, 0f) * localDirection;

            worldDirection.y = 0f;

            if (worldDirection.sqrMagnitude > 0.0001f)
            {
                worldDirection.Normalize();
            }
            else
            {
                worldDirection = Vector3.forward;
            }

            displacement = worldDirection * speed * tickDeltaTime;
            return true;
        }

        private static void UpdateYaw(
            ref CharacterSimulationState state,
            float targetYaw,
            in CharacterSimulationConfig config,
            float tickDeltaTime
        )
        {
            float rotationVelocity = state.RotationSmoothVelocity;

            state.Yaw = Mathf.SmoothDampAngle(
                state.Yaw,
                targetYaw,
                ref rotationVelocity,
                config.RotationSmoothTime,
                float.PositiveInfinity,
                tickDeltaTime
            );

            state.Yaw = Mathf.Repeat(state.Yaw, 360f);
            state.RotationSmoothVelocity = rotationVelocity;
        }

        private static void UpdateSpeed(
            ref CharacterSimulationState state,
            in CharacterSimulationConfig config,
            float tickDeltaTime
        )
        {
            float targetSpeed = config.GetMoveSpeed(state.LocomotionState);

            if (!state.IsGrounded)
            {
                targetSpeed *= config.AirControl;
            }

            float speedVelocity = state.SpeedSmoothVelocity;

            state.SmoothSpeed = Mathf.SmoothDamp(
                state.SmoothSpeed,
                targetSpeed,
                ref speedVelocity,
                config.MoveSpeedSmoothTime,
                float.PositiveInfinity,
                tickDeltaTime
            );

            state.SpeedSmoothVelocity = speedVelocity;
        }

        #endregion

        #region Validation(验证)

        private static void ValidateTickDeltaTime(float tickDeltaTime)
        {
            if(tickDeltaTime <= 0f || float.IsNaN(tickDeltaTime) || float.IsInfinity(tickDeltaTime))
            {
                throw new ArgumentOutOfRangeException(nameof(tickDeltaTime), tickDeltaTime, "模拟 Tick 时长必须是有限的正数。");

            }
        }

        #endregion
    }
}

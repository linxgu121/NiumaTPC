using System;
using NiumaTPC.Character.Motion.MotionEnums;
using UnityEngine;

namespace NiumaTPC.Character.Simulation
{
    /// <summary>
    /// 持有角色模拟状态，并负责执行完整 Tick 和应用权威校正。
    /// 不负责采集输入，也不依赖任何网络框架。
    /// </summary>
    public sealed class CharacterSimulationRunner
    {
        #region Dependencies(依赖项)

        private readonly ICharacterSimulationBody _body;
        private CharacterSimulationConfig _config;

        #endregion

        #region Runtime State(运行时状态)

        private CharacterSimulationState _state;

        public CharacterSimulationState State => _state;

        #endregion

        #region Constructor(构造函数)

        public CharacterSimulationRunner(ICharacterSimulationBody body, in CharacterSimulationConfig config)
        {
            _body = body ?? throw new ArgumentNullException(nameof(body));

            _config = config;

            _state = new CharacterSimulationState(
                tick: 0u,
                position: body.Position,
                yaw: body.Yaw,
                verticalVelocity: 0f,
                locomotionState: LocomotionState.Idle,
                motionPhase: CharacterMotionPhase.Idle,
                motionPhaseTick: 0u,
                startDirection: CharacterStartDirection.Forward,
                startLocomotionState: LocomotionState.Idle,
                lastMoveDirection: Vector3.zero,
                isGrounded: body.IsGrounded,
                hasPerformedDoubleJumpInAir: false,
                smoothSpeed: 0f,
                speedSmoothVelocity: 0f,
                rotationSmoothVelocity: 0f
            );
        }

        #endregion

        #region Simulation(模拟核心逻辑)

        public CharacterSimulationState Simulate(in CharacterInputCommand command, bool canSprint, bool isHandsEmpty, float tickDeltaTime)
        {
            Vector3 displacement = CharacterMovementSimulator.Simulate(
                ref _state,
                in command,
                in _config,
                canSprint,
                isHandsEmpty,
                tickDeltaTime
            );

            _body.SetYaw(_state.Yaw);
            _body.Move(displacement);

            // CharacterController 处理碰撞后，
            // 再把真实结果写回权威状态。
            _state.Position = _body.Position;
            _state.Yaw = _body.Yaw;
            _state.IsGrounded = _body.IsGrounded;

            return _state;
        }

        #endregion

        #region State Correction(状态校正)

        public void ApplyState(in CharacterSimulationState authoritativeState)
        {
             ValidateState(in authoritativeState);

            _body.SetPose(
                authoritativeState.Position,
                authoritativeState.Yaw);

            _state = authoritativeState;

            // SetPose 会标准化 Yaw，因此使用身体实际结果回写。
            _state.Position = _body.Position;
            _state.Yaw = _body.Yaw;

            // 不从 body 覆盖 IsGrounded。
            // CharacterController 在 SetPose 后可能暂时丢失接地缓存，
            // 这里应保留服务器提供的权威接地状态。
        }

        public void SetConfig(in CharacterSimulationConfig config)
        {
            _config = config;
        }

        #endregion

        #region Validation(校验)

        private static void ValidateState(in CharacterSimulationState state)
        {
            bool hasInvalidNumber = !IsFinite(state.Position.x) || !IsFinite(state.Position.y) ||
                !IsFinite(state.Position.z) || !IsFinite(state.Yaw) || !IsFinite(state.VerticalVelocity) ||
                !IsFinite(state.SmoothSpeed) || !IsFinite(state.SpeedSmoothVelocity) || !IsFinite(state.RotationSmoothVelocity)||
                !IsFinite(state.LastMoveDirection.x) || !IsFinite(state.LastMoveDirection.y) || !IsFinite(state.LastMoveDirection.z);

            if (hasInvalidNumber)
            {
                throw new ArgumentException("权威角色状态包含 NaN 或 Infinity。", nameof(state));
            }

            if (!Enum.IsDefined(typeof(LocomotionState), state.LocomotionState))
            {
                throw new ArgumentException($"未知的运动状态：{state.LocomotionState}。", nameof(state));
            }

            if (!Enum.IsDefined(typeof(CharacterMotionPhase), state.MotionPhase))
            {
                throw new ArgumentException($"未知的运动阶段：{state.MotionPhase}。", nameof(state));
            }

            if (!Enum.IsDefined(typeof(CharacterStartDirection), state.StartDirection))
            {
                throw new ArgumentException($"未知的起步方向：{state.StartDirection}。", nameof(state));
            }

            if (!Enum.IsDefined(typeof(LocomotionState), state.StartLocomotionState))
            {
                throw new ArgumentException(
                    $"未知的起步速度档位：{state.StartLocomotionState}。",
                    nameof(state));
            }

        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        #endregion
    }
}

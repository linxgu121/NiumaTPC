using System;
using NiumaTPC.Character.Motion.MotionEnums;
using NiumaTPC.Character.Traversal;
using UnityEngine;

namespace NiumaTPC.Character.Simulation
{
    /// <summary>
    /// 持有角色模拟状态，并负责执行完整 Tick 和应用权威校正。
    /// 不负责采集输入，也不依赖任何网络框架。
    /// </summary>
    public sealed class CharacterSimulationRunner
    {
        #region Constants

        /// <summary>
        /// 请求距离太小时不进行碰墙进度判断。
        /// </summary>
        private const float SlideDistanceEpsilon = 0.001f;

        /// <summary>
        /// 实际前进距离低于请求距离的这个比例时，
        /// 认为滑铲被正面障碍物阻挡。
        /// </summary>
        private const float SlideBlockedProgressRatio = 0.5f;

        #endregion

        #region Dependencies(依赖项)

        private readonly ICharacterSimulationBody _body;
        private CharacterSimulationConfig _config;
        private readonly ICharacterTraversalProbe _traversalProbe;

        #endregion

        #region Runtime State(运行时状态)

        private CharacterSimulationState _state;

        public CharacterSimulationState State => _state;

        #endregion

        #region Constructor(构造函数)

        public CharacterSimulationRunner(
            ICharacterSimulationBody body, 
            ICharacterTraversalProbe traversalProbe,
            in CharacterSimulationConfig config
            )
        {
            _body = body ?? throw new ArgumentNullException(nameof(body));

            _traversalProbe = traversalProbe ?? throw new ArgumentNullException(nameof(traversalProbe));

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
                vaultType: VaultType.None,
                vaultTick: 0u,
                vaultStartPosition: body.Position,
                vaultStartYaw: body.Yaw,
                vaultWallNormal: Vector3.zero,
                vaultLedgePoint: Vector3.zero,
                vaultLandPoint: Vector3.zero,
                vaultTargetYaw: body.Yaw,
                actionType: CharacterActionType.None,
                actionTick: 0u,
                actionDirection: CharacterActionDirection.Forward,
                slideSpeed: 0f,
                pendingSlideJump: false,
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
                _traversalProbe,
                canSprint,
                isHandsEmpty,
                tickDeltaTime
            );

            /*
             * CharacterMovementSimulator 可能在当前 Tick
             * 自然结束 Slide 或通过 Jump 打断它。
             * 这里只记录真正仍由 Slide 提交的位移。
             */
            bool wasSlidingDuringMove = _state.ActionType == CharacterActionType.Slide;

            Vector3 lockedSlideDirection = _state.LastMoveDirection;

            _body.SetYaw(_state.Yaw);

            CharacterBodyMoveResult moveResult = _body.Move(displacement);

            SynchronizeStateFromBody();
            if (wasSlidingDuringMove)
            {
                FinalizeSlideAfterBodyMove(
                    in command,
                    in moveResult,
                    in lockedSlideDirection,
                    isHandsEmpty,
                    tickDeltaTime);
            }
            return _state;
        }

        /// <summary>
        /// 根据 CharacterController 的真实移动结果
        /// 处理滑铲碰墙、离地以及缓存跳跃
        /// </summary>
        private void FinalizeSlideAfterBodyMove(
            in CharacterInputCommand command,
            in CharacterBodyMoveResult moveResult,
            in Vector3 lockedSlideDirection,
            bool isHandsEmpty,
            float tickDeltaTime)
        {
            bool blockedByWall = IsSlideBlocked(in moveResult,in lockedSlideDirection);

            bool leftGround = !_state.IsGrounded;

            if (!blockedByWall && !leftGround)
            {
                return;
            }

            /*
             * 正面碰墙必须清空水平速度。
             * 离开平台边缘则保留剩余滑行速度，
             * 供接下来的空中移动继承。
             */
            bool preserveHorizontalSpeed = !blockedByWall;

            bool hadPendingJump = CharacterSlideResolver.FinishSlide(
                ref _state,
                preserveHorizontalSpeed);

            /*
             * 提前 Jump 后撞墙时，物理安全结束条件
             * 不需要等待最短滑铲时间。
             * 角色仍然接地即可在当前 Tick 消费缓存跳跃。
             */
            if (hadPendingJump && _state.IsGrounded)
            {
                ConsumePostMoveSlideJump(in command,isHandsEmpty,tickDeltaTime);
            }
        }

        /// <summary>
        /// 消费移动之后的滑铲跳跃
        /// </summary>
        private void ConsumePostMoveSlideJump(
            in CharacterInputCommand command,
            bool isHandsEmpty,
            float tickDeltaTime)
        {
            Vector3 jumpDisplacement = CharacterVerticalMovementSimulator.Simulate(
                ref _state,
                in command,
                in _config,
                isHandsEmpty,
                allowJumpInput: false,
                forceJumpInput: true,
                tickDeltaTime: tickDeltaTime);

            /*
             * 只有“滑铲尚未达到最短时间、同时撞墙且缓存了 Jump”
             * 才会在同一 Tick 进行第二次 Move。
             */
            _body.Move(jumpDisplacement);

            SynchronizeStateFromBody();
        }

        /// <summary>
        /// 物理体的最新状态同步回逻辑模拟状态
        /// </summary>
        private void SynchronizeStateFromBody()
        {
            _state.Position = _body.Position;
            _state.Yaw = _body.Yaw;
            _state.IsGrounded = _body.IsGrounded;
        }

        /// <summary>
        /// 判断滑铲是否被正前方墙体阻挡
        /// </summary>
        private static bool IsSlideBlocked(
            in CharacterBodyMoveResult moveResult,
            in Vector3 lockedSlideDirection)
        {
            if (!moveResult.CollidedSides)
            {
                return false;
            }

            Vector3 slideDirection = lockedSlideDirection;

            slideDirection.y = 0f;

            if (slideDirection.sqrMagnitude < SlideDistanceEpsilon * SlideDistanceEpsilon)
            {
                return false;
            }

            slideDirection.Normalize();

            Vector3 requestedHorizontal = moveResult.RequestedDisplacement;

            requestedHorizontal.y = 0f;

            float requestedForwardDistance = Vector3.Dot(requestedHorizontal,slideDirection);

            if (requestedForwardDistance <= SlideDistanceEpsilon)
            {
                return false;
            }

            Vector3 actualHorizontal = moveResult.ActualDisplacement;

            actualHorizontal.y = 0f;

            float actualForwardDistance = Mathf.Max(0f, Vector3.Dot(actualHorizontal,slideDirection));

            /*
             * 仅有 Sides 不足以认定正面撞墙。
             * 沿墙平行移动时，实际前进投影仍接近请求距离。
             */
            return actualForwardDistance < requestedForwardDistance * SlideBlockedProgressRatio;
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
            bool hasInvalidNumber = 
                !IsFiniteVector(state.Position) ||
                !IsFinite(state.Yaw) ||
                !IsFinite(state.VerticalVelocity) ||
                !IsFiniteVector(state.LastMoveDirection) ||
                !IsFinite(state.SlideSpeed) ||
                !IsFinite(state.SmoothSpeed) ||
                !IsFinite(state.SpeedSmoothVelocity) ||
                !IsFinite(state.RotationSmoothVelocity) ||
                !IsFiniteVector(state.VaultStartPosition) ||
                !IsFinite(state.VaultStartYaw) ||
                !IsFiniteVector(state.VaultWallNormal) ||
                !IsFiniteVector(state.VaultLedgePoint) ||
                !IsFiniteVector(state.VaultLandPoint) ||
                !IsFinite(state.VaultTargetYaw);


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

            if (!Enum.IsDefined(typeof(VaultType),state.VaultType))
            {
                throw new ArgumentException($"未知的翻越类型：{state.VaultType}。",nameof(state));
            }

            if (state.VaultType == VaultType.None && state.VaultTick != 0u)
            {
                throw new ArgumentException("没有执行翻越时，VaultTick 必须为 0。",nameof(state));
            }

            if (state.VaultType != VaultType.None && state.ActionType != CharacterActionType.None)
            {
                throw new ArgumentException("翻越不能与 Roll/Dodge 动作同时存在。", nameof(state));
            }

            if (!Enum.IsDefined(typeof(CharacterActionType), state.ActionType))
            {
                throw new ArgumentException($"未知的角色动作类型：{state.ActionType}。", nameof(state));
            }

            if (!Enum.IsDefined(typeof(CharacterActionDirection),state.ActionDirection))
            {
                throw new ArgumentException($"未知的角色动作方向：{state.ActionDirection}。",nameof(state));
            }

            if (state.ActionType == CharacterActionType.None && state.ActionTick != 0u)
            {
                throw new ArgumentException("角色没有执行动作时，ActionTick 必须为 0。",nameof(state));
            }

            if(state.ActionType == CharacterActionType.Slide && state.SlideSpeed < 0)
            {
                throw new ArgumentException("滑铲速度不能为负数。",nameof(state));
            }

            bool hasStaleSlideState = state.ActionType != CharacterActionType.Slide && (Mathf.Abs(state.SlideSpeed) > 0.0001f || state.PendingSlideJump);

            if(hasStaleSlideState)
            {
               throw new ArgumentException("未执行滑铲时，滑铲速度与缓存跳跃必须已经清空。",nameof(state)); 
            }

        }

        private static bool IsFiniteVector(in Vector3 value)
        {
            return IsFinite(value.x) &&
                   IsFinite(value.y) &&
                   IsFinite(value.z);
        }
        
        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        #endregion
    }
}

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
                viewPitch: 0f,
                isAiming: false,
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

        #region Melee Adsorption(近战吸附)

        /// <summary>
        /// 检查当前权威动作是否允许启动吸附，并冻结本次吸附数据
        /// 翻越、Roll、Dodge 和 Slide 期间不能启动吸附
        /// </summary>
        public bool TryBeginMeleeAdsorption(
            in MeleeAdsorptionSimulationRequest request,
            in MeleeAdsorptionMotionProfile motionProfile)
        {
            bool hasConflictingAction =
                _state.VaultType != VaultType.None ||
                _state.ActionType != CharacterActionType.None;

            if (_state.MeleeAdsorption.IsActive ||
                hasConflictingAction)
            {
                return false;
            }

            if (!MeleeAdsorptionSimulationState.TryBegin(
                    in request,
                    in motionProfile,
                    out MeleeAdsorptionSimulationState adsorptionState))
            {
                return false;
            }

            _state.MeleeAdsorption = adsorptionState;

            return true;
        }

        /// <summary>
        /// 取消当前近战吸附并清空冻结的运行数据
        /// </summary>
        public void CancelMeleeAdsorption()
        {
            _state.MeleeAdsorption.Stop();
        }

        #endregion

        #region Simulation(模拟核心逻辑)

        public CharacterSimulationState Simulate(
            in CharacterInputCommand command,
            bool canSprint,
            bool isHandsEmpty,
            float tickDeltaTime)
        {
            // 跳跃模拟会提前修改 IsGrounded，因此必须先保存移动前的状态
            bool wasGroundedBeforeMove = _state.IsGrounded;
            float previousPositionY = _state.Position.y;

            bool wasAdsorbingDuringMove = _state.MeleeAdsorption.IsActive;

            Vector3 displacement;

            if (wasAdsorbingDuringMove)
            {
                bool adsorptionClaimedMovement =
                    TrySimulateMeleeAdsorptionTick(
                        in command,
                        isHandsEmpty,
                        tickDeltaTime,
                        out displacement);

                /*
                 * 无效吸附状态会被模拟器主动清除
                 * 当前 Tick 立即恢复普通移动
                 */
                if (!adsorptionClaimedMovement)
                {
                    wasAdsorbingDuringMove = false;

                    displacement = CharacterMovementSimulator.Simulate(
                        ref _state,
                        in command,
                        in _config,
                        _traversalProbe,
                        canSprint,
                        isHandsEmpty,
                        tickDeltaTime);
                }
            }
            else
            {
                displacement = CharacterMovementSimulator.Simulate(
                    ref _state,
                    in command,
                    in _config,
                    _traversalProbe,
                    canSprint,
                    isHandsEmpty,
                    tickDeltaTime);
            }

            /*
             * CharacterMovementSimulator 可能在当前 Tick
             * 自然结束 Slide 或通过 Jump 打断它
             * 这里只记录真正仍由 Slide 提交的位移
             */
            bool wasSlidingDuringMove = _state.ActionType == CharacterActionType.Slide;

            Vector3 lockedSlideDirection = _state.LastMoveDirection;

            _body.SetYaw(_state.Yaw);

            CharacterBodyMoveResult moveResult = _body.Move(displacement);

            SynchronizeStateFromBody(wasGroundedBeforeMove, previousPositionY);

            if (wasAdsorbingDuringMove)
            {
                MeleeAdsorptionMovementSimulator
                    .FinalizeAfterBodyMove(
                        ref _state.MeleeAdsorption,
                        in moveResult);
            }
            else if (wasSlidingDuringMove)
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
        /// 检查活动吸附状态并生成本 Tick 的吸附水平位移与垂直重力
        /// 吸附接管期间不会响应 Jump、Vault、Roll、Dodge 或 Slide
        /// </summary>
        private bool TrySimulateMeleeAdsorptionTick(
            in CharacterInputCommand command,
            bool isHandsEmpty,
            float tickDeltaTime,
            out Vector3 displacement)
        {
            displacement = Vector3.zero;

            _state.Tick = command.Tick;

            CharacterAimStateResolver.Update(
                ref _state,
                in command,
                in _config);

            bool simulatedAdsorption =
                MeleeAdsorptionMovementSimulator.TrySimulate(
                    ref _state.MeleeAdsorption,
                    _state.Position,
                    _state.Yaw,
                    _body.CollisionRadius,
                    tickDeltaTime,
                    out Vector3 horizontalDisplacement,
                    out float nextYaw);

            if (!simulatedAdsorption)
            {
                return false;
            }

            _state.Yaw = nextYaw;

            Vector3 verticalDisplacement = CharacterVerticalMovementSimulator.Simulate(
                ref _state,
                in command,
                in _config,
                isHandsEmpty,
                allowJumpInput: false,
                forceJumpInput: false,
                tickDeltaTime: tickDeltaTime);

            displacement = horizontalDisplacement + verticalDisplacement;

            return true;
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
            bool blockedByWall = IsSlideBlocked(in moveResult, in lockedSlideDirection);

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
                ConsumePostMoveSlideJump(in command, isHandsEmpty, tickDeltaTime);
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
            // 这是同一 Tick 内可能发生的第二次移动，单独记录它的起始状态
            bool wasGroundedBeforeMove = _state.IsGrounded;
            float previousPositionY = _state.Position.y;

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

            SynchronizeStateFromBody(wasGroundedBeforeMove, previousPositionY);
        }

        /// <summary>
        /// 读取碰撞移动后的实际身体状态，并记录接地转换和落差
        /// </summary>
        private void SynchronizeStateFromBody(
            bool wasGroundedBeforeMove,
            float previousPositionY)
        {
            _state.Position = _body.Position;
            _state.Yaw = _body.Yaw;
            _state.IsGrounded = _body.IsGrounded;

            UpdateFallTracking(wasGroundedBeforeMove, previousPositionY);
        }

        /// <summary>
        /// 根据实际移动前后的接地状态维护最高点，并在落地时保存事件和落差
        /// 不读取动画时间，不在普通接地 Tick 清除上一次落地结果
        /// </summary>
        private void UpdateFallTracking(
            bool wasGroundedBeforeMove,
            float previousPositionY)
        {
            float currentY = _state.Position.y;

            if (!_state.IsGrounded)
            {
                if (wasGroundedBeforeMove)
                {
                    // 刚离地时纳入移动前高度，避免走出平台时少算第一段下降
                    _state.AirborneApexY = Mathf.Max(previousPositionY, currentY);
                }
                else
                {
                    // 二段跳继续抬高最高点，不重新开始一段落差记录
                    _state.AirborneApexY = Mathf.Max(_state.AirborneApexY, currentY);
                }

                return;
            }

            if (!wasGroundedBeforeMove)
            {
                float apexY = Mathf.Max(_state.AirborneApexY, previousPositionY);

                _state.LastLandingFallHeight = Mathf.Max(0f, apexY - currentY);

                // 每次实际落地递增，回绕时跳过表示“尚无事件”的 0
                _state.LandingSequence = unchecked(_state.LandingSequence + 1u);

                if (_state.LandingSequence == 0u)
                {
                    _state.LandingSequence = 1u;
                }
            }

            // 接地期间更新下一次离地的高度基准
            _state.AirborneApexY = currentY;
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

            float requestedForwardDistance = Vector3.Dot(requestedHorizontal, slideDirection);

            if (requestedForwardDistance <= SlideDistanceEpsilon)
            {
                return false;
            }

            Vector3 actualHorizontal = moveResult.ActualDisplacement;

            actualHorizontal.y = 0f;

            float actualForwardDistance = Mathf.Max(0f, Vector3.Dot(actualHorizontal, slideDirection));

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

        /// <summary>
        /// 检查权威状态中的数值、枚举与互斥动作是否可安全用于校正
        /// </summary>
        private static void ValidateState(in CharacterSimulationState state)
        {
            bool hasInvalidNumber =
                !IsFiniteVector(state.Position) ||
                !IsFinite(state.Yaw) ||
                !IsFinite(state.VerticalVelocity) ||
                !IsFinite(state.AirborneApexY) ||
                !IsFinite(state.LastLandingFallHeight) ||
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

            // 落差可以为零，但不能为负数
            if (state.LastLandingFallHeight < 0f)
            {
                throw new ArgumentException("权威角色状态中的落地落差不能为负数", nameof(state));
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
                throw new ArgumentException($"未知的起步速度档位：{state.StartLocomotionState}。",nameof(state));
            }

            if (!Enum.IsDefined(typeof(VaultType), state.VaultType))
            {
                throw new ArgumentException($"未知的翻越类型：{state.VaultType}。", nameof(state));
            }

            if (state.VaultType == VaultType.None && state.VaultTick != 0u)
            {
                throw new ArgumentException("没有执行翻越时，VaultTick 必须为 0。", nameof(state));
            }

            if (state.VaultType != VaultType.None && state.ActionType != CharacterActionType.None)
            {
                throw new ArgumentException("翻越不能与 Roll/Dodge 动作同时存在。", nameof(state));
            }

            if (!Enum.IsDefined(typeof(CharacterActionType), state.ActionType))
            {
                throw new ArgumentException($"未知的角色动作类型：{state.ActionType}。", nameof(state));
            }

            if (!Enum.IsDefined(typeof(CharacterActionDirection), state.ActionDirection))
            {
                throw new ArgumentException($"未知的角色动作方向：{state.ActionDirection}。", nameof(state));
            }

            if (state.ActionType == CharacterActionType.None && state.ActionTick != 0u)
            {
                throw new ArgumentException("角色没有执行动作时，ActionTick 必须为 0。", nameof(state));
            }

            if (state.ActionType == CharacterActionType.Slide && state.SlideSpeed < 0)
            {
                throw new ArgumentException("滑铲速度不能为负数。", nameof(state));
            }

            bool hasStaleSlideState = state.ActionType != CharacterActionType.Slide && (Mathf.Abs(state.SlideSpeed) > 0.0001f || state.PendingSlideJump);

            if (hasStaleSlideState)
            {
                throw new ArgumentException("未执行滑铲时，滑铲速度与缓存跳跃必须已经清空。", nameof(state));
            }

            if (!state.MeleeAdsorption.IsValid)
            {
                throw new ArgumentException("近战吸附状态包含无效请求、运动参数或累计数据", nameof(state));
            }

            bool adsorptionConflictsWithAction =
                state.MeleeAdsorption.IsActive &&
                (state.VaultType != VaultType.None ||
                 state.ActionType != CharacterActionType.None);

            if (adsorptionConflictsWithAction)
            {
                throw new ArgumentException("近战吸附不能与翻越、Roll、Dodge 或 Slide 同时存在", nameof(state));
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

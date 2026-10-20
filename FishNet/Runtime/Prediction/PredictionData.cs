using FishNet.Object.Prediction;
using NiumaTPC.Character.Motion.MotionEnums;
using NiumaTPC.Character.Simulation;
using UnityEngine;

namespace NiumaTPC.FishNet.Prediction
{
    /// <summary>
    /// 拥有者客户端发送给服务器的单 Tick 输入。
    ///
    /// FishNet 会保存这份数据，用于：
    /// 1. 客户端立即预测。
    /// 2. 服务器权威模拟。
    /// 3. 校正后重新执行历史输入。
    /// </summary>
    public struct ReplicateData : IReplicateData
    {
        #region Input Data

        /// <summary>
        /// X 表示左右，Y 表示前后。
        /// </summary>
        public Vector2 Move;

        /// <summary>
        /// 输入产生时的世界视角 Yaw。
        /// 服务器不能读取自己的摄像机，因此必须随输入发送。
        /// </summary>
        public float ViewYaw;

        /// <summary>
        /// 输入产生时的视角俯仰角
        /// 服务器仍会按照自己的 CoreSO 配置再次限幅
        /// </summary>
        public float ViewPitch;

        /// <summary>
        /// Walk、Sprint 等持续按钮状态。
        /// </summary>
        public CharacterInputButtons Buttons;

        /// <summary>
        /// 客户端选择的近战目标 NetworkObject ID
        /// 服务器只把它当作目标提示，不直接认可
        /// </summary>
        public int MeleeTargetObjectId;

        /// <summary>
        /// Owner 预测与历史重演使用的冻结目标位置
        /// 服务器不会把该位置作为权威目标位置
        /// </summary>
        public Vector3 MeleeTargetPosition;

        /// <summary>
        /// Owner 预测与历史重演使用的冻结目标碰撞半径
        /// 服务器会从权威目标组件重新读取
        /// </summary>
        public float MeleeTargetCollisionRadius;

        #endregion

        #region FishNet Tick

        /*
         * 不需要手动给它赋值。
         * 调用 Replicate 方法时，FishNet 会写入对应 Tick。
         */
        private uint _tick;

        #endregion

        #region Constructor

        public ReplicateData(
            Vector2 move,
            float viewYaw,
            float viewPitch,
            CharacterInputButtons buttons,
            int meleeTargetObjectId,
            Vector3 meleeTargetPosition,
            float meleeTargetCollisionRadius)
        {
            Move = move;
            ViewYaw = viewYaw;
            ViewPitch = viewPitch;
            Buttons = buttons;

            MeleeTargetObjectId = meleeTargetObjectId;

            MeleeTargetPosition = meleeTargetPosition;

            MeleeTargetCollisionRadius = meleeTargetCollisionRadius;

            _tick = 0u;
        }

        #endregion

        #region Conversion

        /// <summary>
        /// 转换成网络无关的 NiumaTPC 输入命令。
        /// </summary>
        public CharacterInputCommand ToCommand()
        {
            return new CharacterInputCommand(
                GetTick(),
                Move,
                ViewYaw,
                ViewPitch,
                Buttons);
        }

        #endregion

        #region IReplicateData

        public uint GetTick()
        {
            return _tick;
        }

        public void SetTick(uint value)
        {
            _tick = value;
        }

        public void Dispose()
        {
            // 当前只包含值类型，不需要释放或归还对象池。
        }

        #endregion
    }

    /// <summary>
    /// 服务器发送给拥有者客户端的权威模拟状态。
    ///
    /// 不能只发送 Position，因为速度平滑、旋转平滑和重力
    /// 都会影响后续 Tick 的执行结果。
    /// </summary>
    public struct NiumaReconcileData : IReconcileData
    {
        #region Authoritative State

        public Vector3 Position;

        public float Yaw;

        /// <summary>服务器确认的视角俯仰角。</summary>
        public float ViewPitch;

        /// <summary>服务器确认的持续瞄准状态。</summary>
        public bool IsAiming;

        public float VerticalVelocity;

        /*
         * LocomotionState 当前只有 0～4，
         * 网络包中用 byte 保存可以避免发送完整 int。
         */
        public byte LocomotionStateValue;

        /// <summary>
        /// CharacterMotionPhase 的网络存储值。
        /// </summary>
        public byte MotionPhaseValue;

        /// <summary>
        /// 当前阶段已经执行的 Tick 数。
        /// </summary>
        public uint MotionPhaseTick;

        /// <summary>
        /// CharacterStartDirection 的网络存储值。
        /// </summary>
        public byte StartDirectionValue;

        /// <summary>
        /// 起步瞬间锁定的 LocomotionState 网络存储值。
        /// </summary>
        public byte StartLocomotionStateValue;

        /// <summary>
        /// 上一个有效移动方向，用于反向输入判断。
        /// </summary>
        public Vector3 LastMoveDirection;

        /// <summary>
        /// 是否落地
        /// </summary>
        public bool IsGrounded;

        /// <summary>
        /// 可否执行二连跳
        /// </summary>
        public bool HasPerformedDoubleJumpInAir;

        /// <summary>
        /// VaultType 的网络存储值。
        /// </summary>
        public byte VaultTypeValue;

        public uint VaultTick;

        public Vector3 VaultStartPosition;

        public float VaultStartYaw;

        public Vector3 VaultWallNormal;

        public Vector3 VaultLedgePoint;

        public Vector3 VaultLandPoint;

        public float VaultTargetYaw;

        /// <summary>
        /// CharacterActionType 的网络存储值。
        /// </summary>
        public byte ActionTypeValue;

        /// <summary>
        /// 权威动作已经执行的 Tick 数。
        /// </summary>
        public uint ActionTick;

        /// <summary>
        /// CharacterActionDirection 的网络存储值。
        /// </summary>
        public byte ActionDirectionValue;

        /// <summary>
        /// 服务器确认的当前滑铲剩余速度
        /// </summary>
        public float SlideSpeed;

        /// <summary>
        /// 服务器确认的提前跳跃缓存
        /// </summary>
        public bool PendingSlideJump;

        /// <summary>
        /// 服务器确认的近战吸附活动状态
        /// </summary>
        public bool MeleeAdsorptionIsActive;

        /// <summary>
        /// 吸附所属的攻击开始 Tick
        /// </summary>
        public uint MeleeAdsorptionAttackTick;

        /// <summary>
        /// 吸附开始时冻结的目标世界位置
        /// </summary>
        public Vector3 MeleeAdsorptionTargetPosition;

        /// <summary>
        /// 目标的世界空间水平碰撞半径
        /// </summary>
        public float MeleeAdsorptionTargetCollisionRadius;

        /// <summary>
        /// 本次吸附允许产生的最大总位移
        /// </summary>
        public float MeleeAdsorptionMaximumTravelDistance;

        /// <summary>
        /// 吸附最大水平速度
        /// </summary>
        public float MeleeAdsorptionMaximumSpeed;

        /// <summary>
        /// 吸附最多持续的固定 Tick 数
        /// </summary>
        public uint MeleeAdsorptionDurationTicks;

        /// <summary>
        /// 双方碰撞表面之外保留的停止间隔
        /// </summary>
        public float MeleeAdsorptionStoppingSurfaceGap;

        /// <summary>
        /// 吸附期间每秒允许修正的最大朝向角度
        /// </summary>
        public float MeleeAdsorptionMaximumTurnSpeedDegreesPerSecond;

        /// <summary>
        /// 已经完成的吸附模拟 Tick 数
        /// </summary>
        public uint MeleeAdsorptionElapsedTicks;

        /// <summary>
        /// 吸附期间已经产生的实际水平位移
        /// </summary>
        public float MeleeAdsorptionAccumulatedTravelDistance;

        public float SmoothSpeed;

        public float SpeedSmoothVelocity;

        public float RotationSmoothVelocity;

        #endregion

        #region FishNet Tick

        /*
         * Reconcile 对应的 Tick 也由 FishNet 自动填写。
         * 不重复发送 CharacterSimulationState.Tick。
         */
        private uint _tick;

        #endregion

        #region Constructor

        public NiumaReconcileData(in CharacterSimulationState state)
        {
            Position = state.Position;
            Yaw = state.Yaw;
            ViewPitch = state.ViewPitch;
            IsAiming = state.IsAiming;
            VerticalVelocity = state.VerticalVelocity;
            LocomotionStateValue = (byte)state.LocomotionState;
            MotionPhaseValue = (byte)state.MotionPhase;
            MotionPhaseTick = state.MotionPhaseTick;
            StartDirectionValue = (byte)state.StartDirection;
            StartLocomotionStateValue = (byte)state.StartLocomotionState;
            LastMoveDirection = state.LastMoveDirection;
            IsGrounded = state.IsGrounded;
            HasPerformedDoubleJumpInAir = state.HasPerformedDoubleJumpInAir;
            VaultTypeValue = (byte)state.VaultType;
            VaultTick = state.VaultTick;
            VaultStartPosition = state.VaultStartPosition;
            VaultStartYaw = state.VaultStartYaw;
            VaultWallNormal = state.VaultWallNormal;
            VaultLedgePoint = state.VaultLedgePoint;
            VaultLandPoint = state.VaultLandPoint;
            VaultTargetYaw = state.VaultTargetYaw;
            ActionTypeValue = (byte)state.ActionType;
            ActionTick = state.ActionTick;
            ActionDirectionValue = (byte)state.ActionDirection;
            SlideSpeed = state.SlideSpeed;
            PendingSlideJump = state.PendingSlideJump;
            MeleeAdsorptionIsActive = state.MeleeAdsorption.IsActive;
            MeleeAdsorptionAttackTick = state.MeleeAdsorption.Request.AttackTick;
            MeleeAdsorptionTargetPosition = state.MeleeAdsorption.Request.TargetPosition;
            MeleeAdsorptionTargetCollisionRadius = state.MeleeAdsorption.Request.TargetCollisionRadius;
            MeleeAdsorptionMaximumTravelDistance = state.MeleeAdsorption.Request.MaximumTravelDistance;
            MeleeAdsorptionMaximumSpeed = state.MeleeAdsorption.MotionProfile.MaximumSpeed;
            MeleeAdsorptionDurationTicks = state.MeleeAdsorption.MotionProfile.DurationTicks;
            MeleeAdsorptionStoppingSurfaceGap = state.MeleeAdsorption.MotionProfile.StoppingSurfaceGap;
            MeleeAdsorptionMaximumTurnSpeedDegreesPerSecond = state.MeleeAdsorption.MotionProfile.MaximumTurnSpeedDegreesPerSecond;
            MeleeAdsorptionElapsedTicks = state.MeleeAdsorption.ElapsedTicks;
            MeleeAdsorptionAccumulatedTravelDistance = state.MeleeAdsorption.AccumulatedTravelDistance;
            SmoothSpeed = state.SmoothSpeed;
            SpeedSmoothVelocity = state.SpeedSmoothVelocity;
            RotationSmoothVelocity = state.RotationSmoothVelocity;
            _tick = 0u;
        }

        #endregion

        #region Conversion

        /// <summary>
        /// 将服务器数据恢复成完整模拟状态
        /// 同时恢复会影响后续 Tick 的吸附阶段数据
        /// </summary>
        public CharacterSimulationState ToSimulationState()
        {
            CharacterSimulationState state =
                new CharacterSimulationState(
                    tick: GetTick(),
                    position: Position,
                    yaw: Yaw,
                    viewPitch: ViewPitch,
                    isAiming: IsAiming,
                    verticalVelocity: VerticalVelocity,
                    locomotionState: (LocomotionState)LocomotionStateValue,
                    motionPhase: (CharacterMotionPhase)MotionPhaseValue,
                    motionPhaseTick: MotionPhaseTick,
                    startDirection: (CharacterStartDirection)StartDirectionValue,
                    startLocomotionState: (LocomotionState)StartLocomotionStateValue,
                    lastMoveDirection: LastMoveDirection,
                    isGrounded: IsGrounded,
                    hasPerformedDoubleJumpInAir: HasPerformedDoubleJumpInAir,
                    vaultType: (VaultType)VaultTypeValue,
                    vaultTick: VaultTick,
                    vaultStartPosition: VaultStartPosition,
                    vaultStartYaw: VaultStartYaw,
                    vaultWallNormal: VaultWallNormal,
                    vaultLedgePoint: VaultLedgePoint,
                    vaultLandPoint: VaultLandPoint,
                    vaultTargetYaw: VaultTargetYaw,
                    actionType: (CharacterActionType)ActionTypeValue,
                    actionTick: ActionTick,
                    actionDirection: (CharacterActionDirection)ActionDirectionValue,
                    slideSpeed: SlideSpeed,
                    pendingSlideJump: PendingSlideJump,
                    smoothSpeed: SmoothSpeed,
                    speedSmoothVelocity: SpeedSmoothVelocity,
                    rotationSmoothVelocity: RotationSmoothVelocity);

            if (MeleeAdsorptionIsActive)
            {
                state.MeleeAdsorption = new MeleeAdsorptionSimulationState
                {
                    IsActive = true,

                    Request = new MeleeAdsorptionSimulationRequest(
                            MeleeAdsorptionAttackTick,
                            MeleeAdsorptionTargetPosition,
                            MeleeAdsorptionTargetCollisionRadius,
                            MeleeAdsorptionMaximumTravelDistance),

                    MotionProfile = new MeleeAdsorptionMotionProfile(
                            MeleeAdsorptionMaximumSpeed,
                            MeleeAdsorptionDurationTicks,
                            MeleeAdsorptionStoppingSurfaceGap,
                            MeleeAdsorptionMaximumTurnSpeedDegreesPerSecond),

                    ElapsedTicks = MeleeAdsorptionElapsedTicks,

                    AccumulatedTravelDistance = MeleeAdsorptionAccumulatedTravelDistance
                };
            }

            return state;
        }

        #endregion

        #region IReconcileData

        public uint GetTick()
        {
            return _tick;
        }

        public void SetTick(uint value)
        {
            _tick = value;
        }

        public void Dispose()
        {
            // 当前只包含值类型，不需要释放或归还对象池。
        }

        #endregion
    }
}

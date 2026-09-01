using NiumaTPC.Character.Motion.MotionEnums;
using UnityEngine;

namespace NiumaTPC.Character.Simulation
{
    /// <summary>
    /// 某个模拟 Tick 执行完成后的权威角色状态。
    /// 客户端预测和服务器权威模拟都使用这份数据。
    /// </summary>
    public struct CharacterSimulationState
    {
        /// <summary>
        /// 该状态对应的网络模拟 Tick
        /// </summary>
        public uint Tick;

        /// <summary>
        /// 角色根节点的世界坐标。
        /// </summary>
        public Vector3 Position;

        /// <summary>
        /// 角色绕世界 Y 轴的朝向。
        /// 第一阶段只处理地面移动，所以不保存完整 Quaternion。
        /// </summary>
        public float Yaw;

        /// <summary>
        /// 当前垂直速度，用于继续计算重力与落地
        /// </summary>
        public float VerticalVelocity;

        /// <summary>
        /// 当前运动形态，例如 Idle、Walk、Jog、Sprint。
        /// </summary>
        public LocomotionState LocomotionState;

        /// <summary>
        /// 当前运动阶段。它和 Walk、Jog、Sprint 不是同一个概念。
        /// LocomotionState 表示速度档位，MotionPhase 表示运动过程。
        /// </summary>
        public CharacterMotionPhase MotionPhase;

        /// <summary>
        /// 当前运动阶段已经执行了多少个 Tick。
        /// 进入新阶段时归零。
        /// </summary>
        public uint MotionPhaseTick;

        /// <summary>
        /// 进入 Starting 时确定的八方向起步类型。
        /// Starting 期间保持不变，避免方向轻微波动时反复切换曲线。
        /// </summary>
        public CharacterStartDirection StartDirection;

        /// <summary>
        /// 进入 Starting 瞬间锁定的速度档位。
        /// 起步期间不允许因为按键变化而切换到另一套曲线。
        /// </summary>
        public LocomotionState StartLocomotionState;

        /// <summary>
        /// 上一个有效 Tick 的世界空间移动方向。
        /// 用于检测突然反向，Y 分量始终为零。
        /// </summary>
        public Vector3 LastMoveDirection;

        /// <summary>
        /// 当前是否着地
        /// </summary>
        public bool IsGrounded;

        /// <summary>
        /// 本轮置空是否已经消耗二连跳
        /// 必须进入权威状态，否则 Reconcile 后可能重复获得二段跳
        /// </summary>
        public bool HasPerformedDoubleJumpInAir;

        /// <summary>
        /// 当前正在执行的权威翻越类型。
        /// </summary>
        public VaultType VaultType;

        /// <summary>
        /// 已经执行完成的翻越固定 Tick 数。
        /// </summary>
        public uint VaultTick;

        /// <summary>
        /// 翻越启动时的权威位置。
        /// </summary>
        public Vector3 VaultStartPosition;

        /// <summary>
        /// 翻越启动时的权威朝向。
        /// </summary>
        public float VaultStartYaw;

        /// <summary>
        /// 服务器确认的墙面法线。
        /// </summary>
        public Vector3 VaultWallNormal;

        /// <summary>
        /// 服务器确认的墙沿目标。
        /// </summary>
        public Vector3 VaultLedgePoint;

        /// <summary>
        /// 服务器确认的翻越落点。
        /// </summary>
        public Vector3 VaultLandPoint;

        /// <summary>
        /// 翻越期间需要收敛到的目标朝向。
        /// </summary>
        public float VaultTargetYaw;

        /// <summary>
        /// 当前正在执行的可回滚短时动作
        /// </summary>
        public CharacterActionType ActionType;

        /// <summary>
        /// 当前动作已经执行的固定 Tick 数。
        /// 动作开始时归零，结束时也必须归零。
        /// </summary>
        public uint ActionTick;
        
        /// <summary>
        /// 动作开始瞬间锁定的八方向。
        /// 动作过程中普通移动输入不能修改它
        /// </summary>
        public CharacterActionDirection ActionDirection;

        /// <summary>
        /// 移动速度平滑后的当前值。
        /// 它会影响下一 Tick，不能只保存最终位置。
        /// </summary>
        public float SmoothSpeed;

       /// <summary>
       /// Mathf.SmoothDamp 内部使用的速度累计值。
       /// 回滚时如果不恢复它，重演结果仍然可能不同
       /// </summary>
        public float SpeedSmoothVelocity;

        /// <summary>
        /// Mathf.SmoothDampAngle 使用的旋转速度累计值
        /// </summary>
        public float RotationSmoothVelocity;

        public CharacterSimulationState(
            uint tick,
            Vector3 position,
            float yaw,
            float verticalVelocity,
            LocomotionState locomotionState,
            CharacterMotionPhase motionPhase,
            uint motionPhaseTick,
            CharacterStartDirection startDirection,
            LocomotionState startLocomotionState,
            Vector3 lastMoveDirection,
            bool isGrounded,
            bool hasPerformedDoubleJumpInAir,
            VaultType vaultType,
            uint vaultTick,
            Vector3 vaultStartPosition,
            float vaultStartYaw,
            Vector3 vaultWallNormal,
            Vector3 vaultLedgePoint,
            Vector3 vaultLandPoint,
            float vaultTargetYaw,
            CharacterActionType actionType,
            uint actionTick,
            CharacterActionDirection actionDirection,
            float smoothSpeed,
            float speedSmoothVelocity,
            float rotationSmoothVelocity
        )
        {
            Tick = tick;
            Position = position;
            Yaw = yaw;
            VerticalVelocity = verticalVelocity;
            LocomotionState = locomotionState;
            MotionPhase = motionPhase;
            MotionPhaseTick = motionPhaseTick;
            StartDirection = startDirection;
            StartLocomotionState = startLocomotionState;
            LastMoveDirection = lastMoveDirection;
            IsGrounded = isGrounded;
            HasPerformedDoubleJumpInAir = hasPerformedDoubleJumpInAir;
            VaultType = vaultType;
            VaultTick = vaultTick;
            VaultStartPosition = vaultStartPosition;
            VaultStartYaw = vaultStartYaw;
            VaultWallNormal = vaultWallNormal;
            VaultLedgePoint = vaultLedgePoint;
            VaultLandPoint = vaultLandPoint;
            VaultTargetYaw = vaultTargetYaw;
            ActionType = actionType;
            ActionTick = actionTick;
            ActionDirection = actionDirection;
            SmoothSpeed = smoothSpeed;
            SpeedSmoothVelocity = speedSmoothVelocity;
            RotationSmoothVelocity = rotationSmoothVelocity;
        }
    }
}

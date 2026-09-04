using NiumaTPC.Character.Motion.MotionEnums;
using UnityEngine;

namespace NiumaTPC.Character.Simulation
{
    /// <summary>
    /// 服务器发送给远端观察者的角色表现快照
    /// 它只描述“角色应该表现成什么状态”
    /// 不保存位置预测、平滑累计值或具体动画播放进度
    /// </summary>
    public struct CharacterPresentationState
    {
        /// <summary>
        /// 服务器生成该快照时的网络 Tick
        /// 客户端后续用它拒绝重复包和乱序旧包
        /// </summary>
        public uint Tick;

        /// <summary>
        /// 服务器确认的角色水平朝向。
        /// </summary>
        public float Yaw;

        /// <summary>
        /// 服务器确认的视角俯仰角。
        /// 仅用于远端瞄准动画和 IK 表现。
        /// </summary>
        public float ViewPitch;

        /// <summary>
        /// 服务器确认的持续瞄准状态。
        /// </summary>
        public bool IsAiming;

        /// <summary>
        /// 服务器确认的垂直速度。
        /// 后续供起跳、下落和落地表现使用。
        /// </summary>
        public float VerticalVelocity;

        /// <summary>
        /// 当前移动速度档位，例如 Idle、Walk、Jog、Sprint。
        /// </summary>
        public LocomotionState LocomotionState;

        /// <summary>
        /// 当前运动过程，例如 Starting、Moving、Stopping。
        /// </summary>
        public CharacterMotionPhase MotionPhase;

        /// <summary>
        /// 当前运动阶段已经持续的 Tick 数。
        /// </summary>
        public uint MotionPhaseTick;

        /// <summary>
        /// 本轮起步时锁定的八方向。
        /// </summary>
        public CharacterStartDirection StartDirection;

        /// <summary>
        /// 本轮起步时锁定的移动速度档位。
        /// </summary>
        public LocomotionState StartLocomotionState;

        /// <summary>
        /// 服务器确认的当前动作类型。
        /// </summary>
        public CharacterActionType ActionType;

        /// <summary>
        /// 服务器生成快照时，动作已经执行的 Tick 数。
        /// </summary>
        public uint ActionTick;

        /// <summary>
        /// 动作开始时锁定的八方向。
        /// </summary>
        public CharacterActionDirection ActionDirection;

        /// <summary>
        /// 服务器确认的当前翻越类型
        /// </summary>
        public VaultType VaultType;
        
        /// <summary>
        /// 当前翻越已经执行Tick数
        /// </summary>
        public uint VaultTick;

        /// <summary>服务器确认的翻越墙面法线。</summary>
        public Vector3 VaultWallNormal;

        /// <summary>服务器确认的墙沿根节点目标。</summary>
        public Vector3 VaultLedgePoint;

        /// <summary>
        /// 服务器确认的世界空间移动方向。
        /// 表现层会把它转换成角色局部动画方向。
        /// </summary>
        public Vector3 MoveDirection;

        /// <summary>
        /// 当前是否处于着地状态。
        /// </summary>
        public bool IsGrounded;

        /// <summary>
        /// 本轮滞空是否已经消费二段跳。
        /// 观察客户端通过它的 false -> true 边沿触发二段跳动画。
        /// </summary>
        public bool HasPerformedDoubleJumpInAir;

        /// <summary>
        /// 当前经过平滑后的水平移动速度。
        /// </summary>
        public float Speed;

        /// <summary>
        /// 从完整模拟状态提取表现层真正需要的数据。
        /// </summary>
        public CharacterPresentationState(in CharacterSimulationState simulationState)
        {
            Tick = simulationState.Tick;
            Yaw = simulationState.Yaw;
            ViewPitch = simulationState.ViewPitch;
            IsAiming = simulationState.IsAiming;
            VerticalVelocity = simulationState.VerticalVelocity;
            LocomotionState = simulationState.LocomotionState;
            MotionPhase = simulationState.MotionPhase;
            MotionPhaseTick = simulationState.MotionPhaseTick;
            StartDirection = simulationState.StartDirection;
            StartLocomotionState = simulationState.StartLocomotionState;
            ActionType = simulationState.ActionType;
            ActionTick = simulationState.ActionTick;
            VaultType = simulationState.VaultType;
            VaultTick = simulationState.VaultTick;
            VaultWallNormal = simulationState.VaultWallNormal;
            VaultLedgePoint = simulationState.VaultLedgePoint;
            ActionDirection = simulationState.ActionDirection;
            MoveDirection = simulationState.LastMoveDirection;
            IsGrounded = simulationState.IsGrounded;
            HasPerformedDoubleJumpInAir = simulationState.HasPerformedDoubleJumpInAir;
            Speed = simulationState.SmoothSpeed;
        }
    }
}

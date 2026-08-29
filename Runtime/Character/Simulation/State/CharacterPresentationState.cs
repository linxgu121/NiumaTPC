using NiumaTPC.Character.Motion.MotionEnums;
using UnityEngine;

namespace NiumaTPC.Character.Simulation
{
    /// <summary>
    /// 服务器发送给远端观察者的角色表现快照
    /// 
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
            VerticalVelocity = simulationState.VerticalVelocity;
            LocomotionState = simulationState.LocomotionState;
            MotionPhase = simulationState.MotionPhase;
            MotionPhaseTick = simulationState.MotionPhaseTick;
            StartDirection = simulationState.StartDirection;
            StartLocomotionState = simulationState.StartLocomotionState;
            MoveDirection = simulationState.LastMoveDirection;
            IsGrounded = simulationState.IsGrounded;
            HasPerformedDoubleJumpInAir = simulationState.HasPerformedDoubleJumpInAir;
            Speed = simulationState.SmoothSpeed;
        }
    }
}
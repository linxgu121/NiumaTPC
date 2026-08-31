using System;
using NiumaTPC.Character.Motion.MotionEnums;
using UnityEngine;

namespace NiumaTPC.Character.Simulation
{
    /// <summary>
    /// 基础角色模拟需要的权威参数。
    /// 服务器和预测客户端使用相同结构执行模拟，
    /// 但服务器提供的参数才是最终权威值。
    /// </summary>
    public readonly struct CharacterSimulationConfig
    {
        #region Movenment(移动)

        public readonly float WalkSpeed;
        public readonly float JogSpeed;
        public readonly float SprintSpeed;

        public readonly float MoveSpeedSmoothTime;
        public readonly float RotationSmoothTime;

        #endregion

        #region Physics(物理)

        public readonly float Gravity;

        /// <summary>
        /// 角色着地时保持贴地使用的垂直速度。
        /// 对应现有 CoreSO.ReboundForce。
        /// </summary>
        public readonly float GroundedVerticalVelocity;

        public readonly float AirControl;

        /// <summary>
        /// 地面起跳的时候赋予角色的向上初速度
        /// </summary>
        public readonly float DefaultJumpInitialVelocity;
        public readonly float WalkJumpInitialVelocity;
        public readonly float SprintJumpInitialVelocity;
        public readonly float SprintEmptyJumpInitialVelocity;

        /// <summary>
        /// 普通二段跳初速度。
        /// </summary>
        public readonly float DoubleJumpInitialVelocity;

        /// <summary>
        /// 空手冲刺状态下的二段跳初速度。
        /// </summary>
        public readonly float SprintEmptyDoubleJumpInitialVelocity;

        #endregion

        #region Action Motion(动作位移)

        private readonly CharacterActionMotionProfile _rollMotionProfile;
        private readonly CharacterActionMotionProfile _dodgeMotionProfile;

        #endregion

        #region Constructor(构造的数据快照)

        public CharacterSimulationConfig(
            float walkSpeed,
            float jogSpeed,
            float sprintSpeed,
            float moveSpeedSmoothTime,
            float rotationSmoothTime,
            float gravity,
            float groundedVerticalVelocity,
            float airControl,
            float defaultJumpInitialVelocity,
            float walkJumpInitialVelocity,
            float sprintJumpInitialVelocity,
            float sprintEmptyJumpInitialVelocity,
            float doubleJumpInitialVelocity,
            float sprintEmptyDoubleJumpInitialVelocity,
            CharacterStartMotionProfile[] startMotionProfiles,
            CharacterActionMotionProfile rollMotionProfile,
            CharacterActionMotionProfile dodgeMotionProfile)
        {
            WalkSpeed = Mathf.Max(0f, walkSpeed);
            JogSpeed = Mathf.Max(0f, jogSpeed);
            SprintSpeed = Mathf.Max(0f, sprintSpeed);

            MoveSpeedSmoothTime = Mathf.Max(0f, moveSpeedSmoothTime);
            RotationSmoothTime = Mathf.Max(0f, rotationSmoothTime);

            Gravity = gravity;
            GroundedVerticalVelocity = groundedVerticalVelocity;
            AirControl = Mathf.Clamp01(airControl);

            DefaultJumpInitialVelocity = Mathf.Max(0f, defaultJumpInitialVelocity);
            WalkJumpInitialVelocity =Mathf.Max(0f, walkJumpInitialVelocity);
            SprintJumpInitialVelocity =Mathf.Max(0f, sprintJumpInitialVelocity);
            SprintEmptyJumpInitialVelocity = Mathf.Max(0f, sprintEmptyJumpInitialVelocity);
            DoubleJumpInitialVelocity = Mathf.Max(0f,doubleJumpInitialVelocity);
            SprintEmptyDoubleJumpInitialVelocity = Mathf.Max(0f, sprintEmptyDoubleJumpInitialVelocity);

            _startMotionProfiles = startMotionProfiles != null && startMotionProfiles.Length == StartProfileCount ?
                                   startMotionProfiles : Array.Empty<CharacterStartMotionProfile>();

            _rollMotionProfile = rollMotionProfile;
            _dodgeMotionProfile = dodgeMotionProfile;
        }

        #endregion

        #region Public API

        public bool TryGetStartMotionProfile(
            LocomotionState locomotionState,
            CharacterStartDirection startDirection,
            out CharacterStartMotionProfile profile)
        {
            int locomotionIndex = locomotionState switch
            {
                LocomotionState.Walk => 0,
                LocomotionState.Jog => 1,
                LocomotionState.Sprint => 2,
                _ => -1
            };

            int directionIndex = (int)startDirection;

            bool hasValidIndex = locomotionIndex >= 0 && directionIndex >= 0 &&
                                 directionIndex < StartDirectionCount;
                            
            if(!hasValidIndex || _startMotionProfiles == null || _startMotionProfiles.Length != StartProfileCount)
            {
                profile = default;
                return false;
            }

            int profileIndex = locomotionIndex * StartDirectionCount + directionIndex;

            profile = _startMotionProfiles[profileIndex];
            return profile.IsValid;
        }

        public float GetMoveSpeed(LocomotionState locomotionState)
        {
            return locomotionState switch
            {
                LocomotionState.Walk => WalkSpeed,
                LocomotionState.Jog => JogSpeed,
                LocomotionState.Sprint => SprintSpeed,
                _=> 0f
            };
        }

        /// <summary>
        /// 获取跳跃向上初速度
        /// </summary>
        public float GetJumpInitialVelocity(LocomotionState locomotionState, bool isHandsEmpty)
        {
            switch(locomotionState)
            {
                case LocomotionState.Idle:
                    return DefaultJumpInitialVelocity;

                case LocomotionState.Walk:
                case LocomotionState.Jog:
                    return WalkJumpInitialVelocity;
                
                case LocomotionState.Sprint:
                    return isHandsEmpty ? SprintEmptyJumpInitialVelocity : SprintJumpInitialVelocity;

                default:
                    return DefaultJumpInitialVelocity;
            }
        }

        public float GetDoubleJumpInitialVelocity(LocomotionState locomotionState, bool isHandsEmpty)
        {
            if(locomotionState == LocomotionState.Sprint && isHandsEmpty)
            {
                return SprintEmptyDoubleJumpInitialVelocity;
            }

            return DoubleJumpInitialVelocity;
        }

        /// <summary>
        /// 获取 Roll 或 Dodge 对应的固定 Tick 位移 Profile
        /// 返回 false 表示没有配置该动作或 Profile 无效
        /// </summary>
        public bool TryGetActionMotionProfile(
            CharacterActionType actionType,
            out CharacterActionMotionProfile profile)
        {
            switch (actionType)
            {
                case CharacterActionType.Roll:
                    profile = _rollMotionProfile;
                    break;

                case CharacterActionType.Dodge:
                    profile = _dodgeMotionProfile;
                    break;

                default:
                    profile = default;
                    return false;
            }

            return profile.IsValid;
        }

        #endregion

        #region State Motion

        private const int StartDirectionCount = 8;

        private const int StartLocomotionCount = 3;

        private const int StartProfileCount = StartDirectionCount * StartLocomotionCount;

        private readonly CharacterStartMotionProfile[] _startMotionProfiles;


        #endregion
    }
}
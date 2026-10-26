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

        #region Aiming(瞄准)

        /// <summary>服务器允许的最小视角俯仰角。</summary>
        public readonly float MinimumViewPitch;

        /// <summary>服务器允许的最大视角俯仰角。</summary>
        public readonly float MaximumViewPitch;

        /// <summary>瞄准行走速度，单位 m/s。</summary>
        public readonly float AimWalkSpeed;

        /// <summary>瞄准慢跑速度，单位 m/s。</summary>
        public readonly float AimJogSpeed;

        /// <summary>瞄准冲刺速度，单位 m/s。</summary>
        public readonly float AimSprintSpeed;

        /// <summary>瞄准时身体追随视角 Yaw 的平滑时间。</summary>
        public readonly float AimRotationSmoothTime;

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

        #region Slide Motion(滑铲运动)

        private readonly CharacterSlideMotionProfile _slideMotionProfile;

        #endregion

        #region Constructor(构造的数据快照)

        public CharacterSimulationConfig(
            float walkSpeed,
            float jogSpeed,
            float sprintSpeed,
            float moveSpeedSmoothTime,
            float rotationSmoothTime,
            float minimumViewPitch,
            float maximumViewPitch,
            float aimWalkSpeed,
            float aimJogSpeed,
            float aimSprintSpeed,
            float aimRotationSmoothTime,
            float gravity,
            float groundedVerticalVelocity,
            float airControl,
            float defaultJumpInitialVelocity,
            float walkJumpInitialVelocity,
            float sprintJumpInitialVelocity,
            float sprintEmptyJumpInitialVelocity,
            float doubleJumpInitialVelocity,
            float sprintEmptyDoubleJumpInitialVelocity,
            bool enableVaulting,
            float lowVaultMinHeight,
            float lowVaultMaxHeight,
            float highVaultMinHeight,
            float highVaultMaxHeight,
            CharacterStartMotionProfile[] startMotionProfiles,
            CharacterActionMotionProfile rollMotionProfile,
            CharacterActionMotionProfile dodgeMotionProfile,
            CharacterSlideMotionProfile slideMotionProfile,
            CharacterVaultMotionProfile lowVaultMotionProfile,
            CharacterVaultMotionProfile highVaultMotionProfile)
        {
            WalkSpeed = Mathf.Max(0f, walkSpeed);
            JogSpeed = Mathf.Max(0f, jogSpeed);
            SprintSpeed = Mathf.Max(0f, sprintSpeed);

            MoveSpeedSmoothTime = Mathf.Max(0f, moveSpeedSmoothTime);
            RotationSmoothTime = Mathf.Max(0f, rotationSmoothTime);

            MinimumViewPitch = Mathf.Min(minimumViewPitch,maximumViewPitch);
            MaximumViewPitch = Mathf.Max(minimumViewPitch,maximumViewPitch);
            AimWalkSpeed = Mathf.Max(0f, aimWalkSpeed);
            AimJogSpeed = Mathf.Max(0f, aimJogSpeed);
            AimSprintSpeed = Mathf.Max(0f, aimSprintSpeed);
            AimRotationSmoothTime = Mathf.Max(0f, aimRotationSmoothTime);

            Gravity = gravity;
            GroundedVerticalVelocity = groundedVerticalVelocity;
            AirControl = Mathf.Clamp01(airControl);

            DefaultJumpInitialVelocity = Mathf.Max(0f, defaultJumpInitialVelocity);
            WalkJumpInitialVelocity =Mathf.Max(0f, walkJumpInitialVelocity);
            SprintJumpInitialVelocity =Mathf.Max(0f, sprintJumpInitialVelocity);
            SprintEmptyJumpInitialVelocity = Mathf.Max(0f, sprintEmptyJumpInitialVelocity);
            DoubleJumpInitialVelocity = Mathf.Max(0f,doubleJumpInitialVelocity);
            SprintEmptyDoubleJumpInitialVelocity = Mathf.Max(0f, sprintEmptyDoubleJumpInitialVelocity);
            
            EnableVaulting = enableVaulting;
            LowVaultMinHeight = Mathf.Max(0f, lowVaultMinHeight);
            LowVaultMaxHeight = Mathf.Max(LowVaultMinHeight, lowVaultMaxHeight);
            HighVaultMinHeight = Mathf.Max(0f, highVaultMinHeight);
            HighVaultMaxHeight =Mathf.Max(HighVaultMinHeight, highVaultMaxHeight);

            _startMotionProfiles = startMotionProfiles != null && startMotionProfiles.Length == StartProfileCount ?
                                   startMotionProfiles : Array.Empty<CharacterStartMotionProfile>();


            _rollMotionProfile = rollMotionProfile;
            _dodgeMotionProfile = dodgeMotionProfile;

            _slideMotionProfile = slideMotionProfile;


            _lowVaultMotionProfile = lowVaultMotionProfile;
            _highVaultMotionProfile = highVaultMotionProfile;
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

        /// <summary>
        /// 获取固定 Tick 滑铲运动配置。
        /// 返回 false 表示滑铲未启用、未配置或配置无效。
        /// </summary>
        public bool TryGetSlideMotionProfile(out CharacterSlideMotionProfile profile)
        {
            profile  = _slideMotionProfile;
            return profile.IsValid;
        }

        /// <summary>
        /// 获取 Low 或 High 对应的固定 Tick 翻越轨迹。
        /// </summary>
        public bool TryGetVaultMotionProfile(
            VaultType vaultType,
            out CharacterVaultMotionProfile profile)
        {
            switch (vaultType)
            {
                case VaultType.Low:
                    profile = _lowVaultMotionProfile;
                    break;

                case VaultType.High:
                    profile = _highVaultMotionProfile;
                    break;

                default:
                    profile = default;
                    return false;
            }

            return profile.IsValid;
        }

        /// <summary>
        /// 使用服务器配置限制视角俯仰角。
        /// </summary>
        public float ClampViewPitch(float viewPitch)
        {
            if (float.IsNaN(viewPitch) || float.IsInfinity(viewPitch))
            {
                return Mathf.Clamp(0f,MinimumViewPitch,MaximumViewPitch);
            }

            return Mathf.Clamp(viewPitch,MinimumViewPitch,MaximumViewPitch);
        }

        /// <summary>
        /// 获取瞄准状态下对应移动档位的权威速度。
        /// </summary>
        public float GetAimMoveSpeed(LocomotionState locomotionState)
        {
            return locomotionState switch
            {
                LocomotionState.Walk => AimWalkSpeed,
                LocomotionState.Jog => AimJogSpeed,
                LocomotionState.Sprint => AimSprintSpeed,
                _ => 0f
            };
        }

        #endregion

        #region State Motion

        private const int StartDirectionCount = 8;

        private const int StartLocomotionCount = 3;

        private const int StartProfileCount = StartDirectionCount * StartLocomotionCount;

        private readonly CharacterStartMotionProfile[] _startMotionProfiles;


        #endregion

        #region Vault Motion

        private readonly CharacterVaultMotionProfile _lowVaultMotionProfile;

        private readonly CharacterVaultMotionProfile _highVaultMotionProfile;

        #endregion

        #region Vault Detection

        public readonly bool EnableVaulting;

        public readonly float LowVaultMinHeight;
        public readonly float LowVaultMaxHeight;
        public readonly float HighVaultMinHeight;
        public readonly float HighVaultMaxHeight;

        #endregion
    }
}
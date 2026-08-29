using System;
using UnityEngine;

namespace NiumaTPC.Character.Simulation
{
    /// <summary>
    /// 计算单个模拟 Tick 的重力与垂直位移。
    /// 不负责检测地面，也不直接移动角色。
    /// </summary>
    public static class CharacterVerticalMovementSimulator
    {
        #region Public API
        /// <summary>
        /// 推进垂直速度，并返回本 Tick 的垂直位移
        /// </summary>
        public static Vector3 Simulate(
            ref CharacterSimulationState state,
            in CharacterInputCommand command,
            in CharacterSimulationConfig config,
            bool isHandsEmpty,
            float tickDeltaTime
        )
        {
            ValidateTickDeltaTime(tickDeltaTime);

            bool wantsToJump = command.HasButton(CharacterInputButtons.Jump);

            //一旦回到地面就恢复二连跳次数
            if(state.IsGrounded)
            {
                state.HasPerformedDoubleJumpInAir = false;
            }

            if(wantsToJump && state.IsGrounded)
            {
                //第一次起跳
                state.VerticalVelocity = config.GetJumpInitialVelocity(state.LocomotionState,isHandsEmpty);
                state.IsGrounded = false;
            }
            else if(wantsToJump && !state.HasPerformedDoubleJumpInAir)
            {
                //二连跳
                state.VerticalVelocity = config.GetDoubleJumpInitialVelocity(state.LocomotionState,isHandsEmpty);
                state.HasPerformedDoubleJumpInAir = true;
            }
            else if(state.IsGrounded && state.VerticalVelocity < 0f)
            {
                state.VerticalVelocity = config.GroundedVerticalVelocity;
            }
            else
            {
                state.VerticalVelocity += config.Gravity * tickDeltaTime;
            }

            return Vector3.up * state.VerticalVelocity * tickDeltaTime;
        }

        #endregion

        #region Validation

        private static void ValidateTickDeltaTime(float tickDeltaTime)
        {
             if (tickDeltaTime <= 0f || float.IsNaN(tickDeltaTime) || float.IsInfinity(tickDeltaTime))
            {
                throw new ArgumentOutOfRangeException(nameof(tickDeltaTime),tickDeltaTime,"模拟 Tick 时长必须是有限的正数。");
            }
        }

        #endregion
    }
}
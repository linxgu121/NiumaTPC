using NiumaTPC.Character.State.Base;
using NiumaTPC.Character.State.Core.Aiming;
using NiumaTPC.Character.State.Core.Locomotion;
using UnityEngine;


namespace NiumaTPC.Character.State.Core.Interceptors
{
    /// <summary>
    /// 瞄准全局拦截器
    /// 负责瞄准模式的全局启用与禁用 优先级在运动状态之上
    /// 按住右键时强制切到瞄准状态 松开时回到普通移动
    /// </summary>
    [CreateAssetMenu(fileName = "AimInterceptor", menuName = "NiumaTPC/Player/Interceptors/Aim")]
    public class AimInterceptorSO : StateInterceptorSO
    {
        public override bool TryIntercept(NiumaCharacterController player, PlayerBaseState currentState, out PlayerBaseState nextState)
        {
            nextState = null;
            var data = player.RuntimeData;

            if (data.IsAiming)
            {
                if(currentState is PlayerAimIdleState || currentState is PlayerAimMoveState)
                  return false;

                if(currentState is PlayerJumpState || currentState is PlayerDoubleJumpState ||
                   currentState is PlayerLandState || currentState is PlayerVaultState)
                   return false;

                nextState = data.CurrentLocomotionState == Motion.MotionEnums.LocomotionState.Idle 
                  ? player.StateRegistry.GetState<PlayerAimIdleState>() 
                  : player.StateRegistry.GetState<PlayerAimMoveState>();
                return false;
            }

            return false;
        }
    }
}

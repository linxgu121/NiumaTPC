using NiumaTPC.Character.State.Core.Base.UpperBody;
using NiumaTPC.Character.State.Core.Locomotion;
using NiumaTPC.Character.State.UpperBody;
using UnityEngine;

namespace NiumaTPC.Character.State.Core.Interceptors
{
    public class EnterUnavailableInterceptorSO : UpperBodyInterceptorSO
    {
        public override bool TryIntercept(NiumaCharacterController player, UpperBodyBaseState currentState, out UpperBodyBaseState nextState)
        {
            nextState = null;

            // 1. 如果当前已经在 Unavailable 状态 不要重复进入
            if(currentState != null && currentState is UpperBodyUnavailableState)
            {
                return false;
            }

            // 2. 获取下半身的当前状态 判断是否需要禁用上半身
            var playerbasestate = player.StateMachine.CurrentState;

            // 3. 进行判断 如果是 Vault Fall Roll 状态 禁用上半身
            if (playerbasestate is PlayerVaultState || playerbasestate is PlayerFallState || playerbasestate is PlayerRollState)
            {
                // 获取不可用 Unavailable 状态
                nextState = player.UpperBodyController.StateRegistry.GetState<UpperBodyUnavailableState>();
                return true;
            }

            return false;
        }
    }
}
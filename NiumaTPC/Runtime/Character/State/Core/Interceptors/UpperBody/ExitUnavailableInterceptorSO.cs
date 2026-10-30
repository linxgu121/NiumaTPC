using NiumaTPC.Character.State.Core.Base.UpperBody;
using NiumaTPC.Character.State.Core.Locomotion;
using NiumaTPC.Character.State.UpperBody;
using UnityEngine;

namespace NiumaTPC.Character.State.Core.Interceptors
{
    /// <summary>
    /// 半身退出不可用拦截器
    /// 当下半身脱离翻越或翻滚等强制全身动作后 恢复上半身控制权
    /// </summary>
    [CreateAssetMenu(fileName = "ExitUnavailableInterceptor", menuName = "NiumaTPC/Player/Interceptors/UpperBody/ExitUnavailable")]
    public class ExitUnavailableInterceptorSO : UpperBodyInterceptorSO
    {
        public override bool TryIntercept(NiumaCharacterController player, UpperBodyBaseState currentState, out UpperBodyBaseState nextState)
        {
            nextState = null;

            // 1. 如果当前不是 Unavailable 状态 不需要处理
            if (currentState == null || currentState is not UpperBodyUnavailableState)
            {
                return false;
            }

            // 2. 获取下半身状态 如果还在 Unavailable 的触发条件中 继续保持不可用
            var playerBaseState = player.StateMachine.CurrentState;

            // 翻越和翻滚结束前继续保持上半身不可用
            if (playerBaseState is PlayerVaultState || playerBaseState is PlayerRollState)
            {
                return false;
            }

            // 3. 根据当前是否装备物品 决定回到 HoldItem 还是 Empty
            if (player.RuntimeData != null && player.RuntimeData.CurrentItem != null)
            {
                nextState = player.UpperBodyController.StateRegistry.GetState<UpperBodyHoldItemState>();
            }
            else
            {
                nextState = player.UpperBodyController.StateRegistry.GetState<UpperBodyEmptyState>();
            }

            return true;
        }
    }
}
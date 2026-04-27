using NiumaTPC.Character.State.Base;
using NiumaTPC.Character.State.Core.Locomotion;
using UnityEngine;


namespace NiumaTPC.Character.State.Core.Interceptors
{
    /// <summary>
    /// 跳跃全局拦截器
    /// 统一处理“跳跃意图 -> 进入跳跃状态”的强制转移，避免各状态内重复检测
    /// </summary>
    [CreateAssetMenu(fileName = "JumpInterceptor", menuName = "NiumaTPC/Player/Interceptors/Jump")]
    public class JumpInterceptorSO : StateInterceptorSO
    {
        public override bool TryIntercept(NiumaCharacterController player, PlayerBaseState currentState, out PlayerBaseState nextState)
        {
            nextState = null;
            var data = player.RuntimeData;
            var config = player.Config;

            if(data == null || config == null) return false;

            if(!data.WantsDoubleJump) return false;

            //判断无法进行跳跃动作的特殊动作
            if(currentState is PlayerJumpState || currentState is PlayerDoubleJumpState ||
            currentState is PlayerVaultState || currentState is PlayerFallState || 
            currentState is PlayerRollState || currentState is PlayerDodgeState)
            {
                return false;
            }

            data.NextStatePlayOptions = config.LocomotionAnims != null 
            ? config.LocomotionAnims.FadeInFallOptions : data.NextStatePlayOptions;

            //清除跳跃意图，避免本帧后重复触发
            data.WantsToJump = false;

            nextState = player.StateRegistry.GetState<PlayerJumpState>();
            return nextState != null;
        }
    }
}
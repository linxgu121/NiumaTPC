using NiumaTPC.Character.State.Base;
using NiumaTPC.Character.State.Core.Locomotion;
using UnityEngine;

namespace NiumaTPC.Character.State.Core.Interceptors
{
    /// <summary>
    /// 下落全局拦截器
    /// 负责检测下落意图 当空中时间过长且向下速度大于配置值时自动触发下落动画 优先级较高
    /// </summary>
    [CreateAssetMenu(fileName = "FallInterceptor", menuName = "NiumaTPC/Player/Interceptors/Fall")]
    public class FallInterceptorSO : StateInterceptorSO
    {
        public override bool TryIntercept(NiumaCharacterController player, PlayerBaseState currentState, out PlayerBaseState nextState)
        {
            nextState = null;
            var data = player.RuntimeData;
            var config = player.Config;

            //检测下落意图和垂直速度
            if(data.WantsToFire && data.VerticalVelocity < config.Core.FallVerticalVelocityThreshold &&
            currentState is not PlayerFallState && currentState is not PlayerVaultState)
            {
                data.NextStatePlayOptions = config.LocomotionAnims.FadeInFallOptions;
                nextState = player.StateRegistry.GetState<PlayerFallState>();
                return true;
            }
            return false;
        }
    }
}


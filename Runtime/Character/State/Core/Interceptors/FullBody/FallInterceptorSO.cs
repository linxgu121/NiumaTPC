using NiumaTPC.Character.State.Base;
using NiumaTPC.Character.State.Core.Locomotion;
using UnityEngine;

namespace NiumaTPC.Character.State.Core.Interceptors
{
    /// <summary>
    /// 下落全局拦截器
    /// 根据模拟结果产生的下落意图和垂直速度切入下落动画
    /// 不再累计滞空时间
    /// </summary>
    [CreateAssetMenu(fileName = "FallInterceptor", menuName = "NiumaTPC/Player/Interceptors/Fall")]
    public class FallInterceptorSO : StateInterceptorSO
    {
        // 检查下落意图与速度阈值，避免重复进入下落状态或打断翻越
        public override bool TryIntercept(NiumaCharacterController player, PlayerBaseState currentState, out PlayerBaseState nextState)
        {
            nextState = null;
            var data = player.RuntimeData;
            var config = player.Config;

            //检测下落意图和垂直速度
            if(data.WantsToFall && data.VerticalVelocity < config.Core.FallVerticalVelocityThreshold &&
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


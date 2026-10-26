using NiumaTPC.Character.State.Base;
using NiumaTPC.Character.State.Core.Locomotion;
using UnityEngine;

namespace NiumaTPC.Character.State.Core.Interceptors
{
    /// <summary>
    /// 落地全局拦截器 
    /// 当下落高度等级大于0时触发落地缓冲动画
    /// </summary>
    [CreateAssetMenu(fileName = "LandInterceptor", menuName = "NiumaTPC/Player/Interceptors/Land")]
    public class LandInterceptorSO : StateInterceptorSO
    {
        public override bool TryIntercept(NiumaCharacterController player, PlayerBaseState currentState, out PlayerBaseState nextState)
        {
            nextState = null;
            var data = player.RuntimeData;

            //检测刚刚落地 如果下落高度等级大于0 则切换到落地状态播放缓冲动画
            if(data.JustLanded && data.FallHeightLevel >0 && currentState is not PlayerLandState)
            {
                nextState = player.StateRegistry.GetState<PlayerLandState>();
                return true;
            }

            return false;
        }
    }
}
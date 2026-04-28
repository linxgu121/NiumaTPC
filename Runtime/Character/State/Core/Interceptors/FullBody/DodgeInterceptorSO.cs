using NiumaTPC.Character.State.Base;
using NiumaTPC.Character.State.Core.Locomotion;
using UnityEngine;

namespace NiumaTPC.Character.State.Core.Interceptors
{
    /// <summary>
    /// 闪避全局拦截器
    /// 负责检测闪避输入 并根据前一个运动状态选择对应的闪避淡入参数
    /// </summary>
    [CreateAssetMenu(fileName = "DodgeInterceptor", menuName = "NiumaTPC/Player/Interceptors/Dodge")]
    public class DodgeInterceptorSO : StateInterceptorSO
    {
        public override bool TryIntercept(NiumaCharacterController player, PlayerBaseState currentState, out PlayerBaseState nextState)
        {
            nextState = null;
            var data = player.RuntimeData;

            //检测闪避输入 根据前一个运动状态选择淡入参数
            if (data.WantsToDodge)
            {
                // 从冲刺状态闪避选择不同的参数 更加剧烈的动作
                data.NextStatePlayOptions = data.LastLocomotionState == Motion.MotionEnums.LocomotionState.Sprint ?
                  player.Config.LocomotionAnims.FadeInMoveDodgeOptions :
                  player.Config.LocomotionAnims.FadeInQuickDodgeOptions;

                nextState = player.StateRegistry.GetState<PlayerDodgeState>();
                return true;
            }
            return false;
        }
    }
}
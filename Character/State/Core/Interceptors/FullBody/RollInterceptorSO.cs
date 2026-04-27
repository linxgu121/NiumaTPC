using NiumaTPC.Character.Motion.MotionEnums;
using NiumaTPC.Character.State.Base;
using NiumaTPC.Character.State.Core.Locomotion;
using UnityEngine;

namespace NiumaTPC.Character.State.Core.Interceptors
{
    /// <summary>
    /// 翻滚全局拦截器
    /// 负责检测翻滚输入 并根据前一个运动状态选择对应的翻滚淡入参数
    /// </summary>
    [CreateAssetMenu(fileName = "RollInterceptor", menuName = "NiumaTPC/Player/Interceptors/Roll")]
    public class RollInterceptorSO : StateInterceptorSO
    {
        public override bool TryIntercept(NiumaCharacterController player, PlayerBaseState currentState, out PlayerBaseState nextState)
        {
            nextState = null;
            var data = player.RuntimeData;

            // 检测翻滚输入 根据前一个运动状态选择淡入参数
            if (data.WantsToRoll)
            {
                // 从冲刺状态翻滚选择不同的参数 更加剧烈的动作
                data.NextStatePlayOptions = data.LastLocomotionState == LocomotionState.Sprint ?
                    player.Config.LocomotionAnims.FadeInMoveDodgeOptions :
                    player.Config.LocomotionAnims.FadeInQuickDodgeOptions;

                nextState = player.StateRegistry.GetState<PlayerRollState>();
                return true;
            }

            return false;
        }
    }
}

using NiumaTPC.Character.Simulation;
using NiumaTPC.Character.State.Base;
using NiumaTPC.Character.State.Core.Locomotion;
using UnityEngine;

namespace NiumaTPC.Character.State.Core.Interceptors
{
    /// <summary>
    /// 滑铲表现拦截器。
    /// 只跟随固定 Tick 已确认的 Slide 状态，
    /// 不在表现层重新判断速度、接地或 Sprint 条件。
    /// </summary>
    [CreateAssetMenu(fileName = "SlideInterceptor",menuName = "NiumaTPC/Player/Interceptors/Slide")]
    public class SlideInterceptorSO : StateInterceptorSO
    {
        public override bool TryIntercept(
            NiumaCharacterController player,
            PlayerBaseState currentState,
            out PlayerBaseState nextState)
        {
            nextState = null;

            var data = player.RuntimeData;

            if (data == null ||
                !data.WantsToSlide ||
                data.SimulationActionType != CharacterActionType.Slide ||
                currentState is PlayerSlideState)
            {
                return false;
            }

            nextState =
                player.StateRegistry.GetState<PlayerSlideState>();

            if (nextState == null)
            {
                return false;
            }

            // Enter 会再次清理，这里先清理可以防止同帧重复消费。
            data.WantsToSlide = false;
            return true;
        }
    }
}

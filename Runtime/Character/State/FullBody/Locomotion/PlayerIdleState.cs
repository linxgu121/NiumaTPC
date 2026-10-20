using System;
using NiumaTPC.Character.Core.Animation;
using NiumaTPC.Character.Motion.MotionEnums;


namespace NiumaTPC.Character.State.Core.Locomotion
{
    [Serializable]
    public class PlayerIdleState : PlayerBaseState
    {
        public PlayerIdleState(NiumaCharacterController player) : base(player) { }

        // 进入状态 播放空闲动画 设置平滑淡入时长避免动画跳变
        public override void Enter()
        {
            ChooseOptionsAndPlay(config.LocomotionAnims.IdleAnim);
        }

        // 更新状态逻辑 检测移动 触发状态切换
        // 跳跃由全局拦截器统一处理，避免各状态重复判断
        protected override void UpdateStateLogic()
        {
            if (data.WantsToVault)
            {
                // 兜底处理：即使 Brain 没挂 VaultInterceptor，待机状态也能响应翻越意图。
                data.NextStatePlayOptions = config.LocomotionAnims.FadeInVaultOptions;
                player.StateMachine.ChangeState(player.StateRegistry.GetState<PlayerVaultState>());
                return;
            }

            if (data.CurrentLocomotionState != LocomotionState.Idle)
            {
                switch (data.CurrentLocomotionState)
                {
                    case LocomotionState.Walk:
                        data.NextStatePlayOptions = config.LocomotionAnims.FadeInWalkStartOptions;
                        break;
                    case LocomotionState.Jog:
                        data.NextStatePlayOptions = config.LocomotionAnims.FadeInRunStartOptions;
                        break;
                    case LocomotionState.Sprint:
                        data.NextStatePlayOptions = config.LocomotionAnims.FadeInSprintStartOptions;
                        break;
                    default:
                        data.NextStatePlayOptions = AnimPlayOptions.Default;
                        break;
                }

                player.StateMachine.ChangeState(player.StateRegistry.GetState<PlayerMoveStartState>());
                return;
            }
        }

        // 位移、重力和接地由固定 Tick 模拟处理，表现状态不重复推进
        public override void PhysicsUpdate()
        {
        }

        // 退出状态 空闲状态退出时无额外清理逻辑
        public override void Exit()
        {
        }
    }
}

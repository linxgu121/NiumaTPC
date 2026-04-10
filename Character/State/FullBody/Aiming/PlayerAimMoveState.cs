using System.Collections;
using System.Collections.Generic;
using NiumaTPC.Item.Core.Animation;
using NiumaTPC.Item.Motion.MotionEnums;
using NiumaTPC.Item.State.FullBody.Locomotion;
using NiumaTPC.Core.StateMachine;
using UnityEngine;

namespace NiumaTPC.Item.State.FullBody.Aiming
{
    public class PlayerAimMoveState : PlayerBaseState
    {
        public PlayerAimMoveState(NiumaCharacterController player) : base(player) { }

        private LocomotionState _lastTreeState = LocomotionState.Idle;

        // 进入状态：根据当前运动档位选择对应混合树
        public override void Enter()
        {
            _lastTreeState = LocomotionState.Idle;
            SwitchTreeIfNeeded(force: true);

            // 进入时先写一帧参数 防止混合树停留在默认点
            AnimationFacade.SetMixerParameter(new Vector2(data.CurrentAnimBlendX, data.CurrentAnimBlendY));
        }

        // 状态逻辑 检测松开瞄准 跳跃 或停止输入
        protected override void UpdateStateLogic()
        {
            if (!data.IsAiming)
            {
                player.StateMachine.ChangeState(
                    data.CurrentLocomotionState == LocomotionState.Idle
                        ? (StateBase)player.StateRegistry.GetState<PlayerIdleState>()
                        : player.StateRegistry.GetState<PlayerMoveLoopState>());
                return;
            }

            if (data.WantsDoubleJump)
            {
                player.StateMachine.ChangeState(player.StateRegistry.GetState<PlayerDoubleJumpState>());
                return;
            }

            if (data.WantsToJump)
            {
                player.StateMachine.ChangeState(player.StateRegistry.GetState<PlayerJumpState>());
                return;
            }

            if (data.CurrentLocomotionState == LocomotionState.Idle)
            {
                player.StateMachine.ChangeState(player.StateRegistry.GetState<PlayerAimIdleState>());
                return;
            }

            // 允许在 AimMove 内热切换动画树
            SwitchTreeIfNeeded(force: false);

            // 每帧更新混合树参数（统一圆内坐标）
            AnimationFacade.SetMixerParameter(new Vector2(data.CurrentAnimBlendX, data.CurrentAnimBlendY));
        }

        // 物理更新 在瞄准时仍需处理重力
        public override void PhysicsUpdate()
        {
            player.MotionDriver.UpdateMotion(null, 0f);
        }

        public override void Exit()
        {
            // 保持原先的风格：Aim状态下由状态机管理IK开关
            // 这里不做额外清理 避免与AimIdle逻辑互相踩踏
        }

        // 根据当前locomotion档位选择动画树
        private void SwitchTreeIfNeeded(bool force)
        {
            // AimMove 中 Idle 不应出现（Idle 会被上层逻辑切走）
            // 这里仍做防御：若进入则使用 Jog 树兜底
            LocomotionState desired = data.CurrentLocomotionState;
            if (desired == LocomotionState.Idle)
                desired = LocomotionState.Jog;

            if (!force && desired == _lastTreeState)
                return;

            _lastTreeState = desired;

            var options = AnimPlayOptions.Default;
            options.FadeDuration = 0.12f;

            // 三树：Walk / Jog / Sprint
            var aiming = config.Aiming;
            if (aiming == null)
            {
                Debug.LogError("[PlayerAimMoveState] 致命错误 未配置 AimingModuleSO");
                return;
            }

            object tree = desired switch
            {
                LocomotionState.Walk => aiming.AimWalkMixer,
                LocomotionState.Jog => aiming.AimJogMixer,
                LocomotionState.Sprint => aiming.AimSprintMixer,
                _ => aiming.AimJogMixer
            };

            AnimationFacade.PlayTransition(tree, options);
        }

    }
}

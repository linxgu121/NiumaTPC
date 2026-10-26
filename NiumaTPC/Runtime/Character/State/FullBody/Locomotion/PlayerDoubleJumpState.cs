using NiumaTPC.Character.Config;
using NiumaTPC.Character.Motion.MotionEnums;
using UnityEngine;

namespace NiumaTPC.Character.State.Core.Locomotion
{
    /// <summary>
    /// 二段跳表现状态，根据模拟结果播放对应动画并处理落地表现切换
    /// 不施加速度、不消费输入、不修改二段跳次数
    /// </summary>
    public class PlayerDoubleJumpState : PlayerBaseState
    {
        private MotionClipData _clipData;
        private bool _canCheckLand;

        public PlayerDoubleJumpState(NiumaCharacterController player) : base(player) { }

        // 进入表现状态，选择并播放二段跳动画
        public override void Enter()
        {
            _canCheckLand = false;

            SelectDoubleJumpAnimation();
            ChooseOptionsAndPlay(_clipData.Clip);

            AnimationFacade.SetOnEndCallback(() =>
            {
                if (player.CharacterController.isGrounded)
                {
                    player.StateMachine.ChangeState(player.StateRegistry.GetState<PlayerLandState>());
                }
            });
        }

        // 根据运动状态和装备选择二段跳动画
        private void SelectDoubleJumpAnimation()
        {
            bool isHandsEmpty = data.CurrentItem == null;

            // 根据当前运动状态和装备选择 
            MotionClipData baseClip = null;

            switch (data.CurrentLocomotionState)
            {
                case LocomotionState.Idle:
                case LocomotionState.Walk:
                case LocomotionState.Jog:
                    baseClip = config.JumpAndLanding.DoubleJumpUp;
                    break;

                case LocomotionState.Sprint:
                    if (isHandsEmpty)
                    {
                        baseClip = config.JumpAndLanding.DoubleJumpSprintRoll;
                    }
                    else
                    {
                        baseClip = config.JumpAndLanding.DoubleJumpUp;
                    }
                    break;

                default:
                    Debug.Log(" DoubleJumpUp 配置缺失 使用 JumpAirAnim 作为后备");
                    baseClip = config.JumpAndLanding.JumpAirAnim;
                    break;
            }
            _clipData = baseClip;
        }

        // 状态逻辑 检测落地时机
        protected override void UpdateStateLogic()
        {
            // 给动画启动后 0.2s 才开始检测落地
            if (!_canCheckLand && AnimationFacade.CurrentTime > 0.2f)
            {
                _canCheckLand = true;
            }

            // 检测落地
            if (_canCheckLand && data.VerticalVelocity <= 0 && player.CharacterController.isGrounded)
            {
                player.StateMachine.ChangeState(player.StateRegistry.GetState<PlayerLandState>());
            }
        }

        public override void PhysicsUpdate()
        {
            // 二段跳位移和重力由固定 Tick 模拟推进
        }

        // 退出状态 清理回调和动画数据
        public override void Exit()
        {
            AnimationFacade.ClearOnEndCallback();
            _clipData = null;
        }
    }
}
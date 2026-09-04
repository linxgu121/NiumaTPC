using NiumaTPC.Character.Config;
using NiumaTPC.Character.Core.Animation;
using NiumaTPC.Character.Motion.MotionEnums;
using NiumaTPC.Character.Simulation;
using UnityEngine;

namespace NiumaTPC.Character.State.Core.Locomotion
{
    /// <summary>
    /// 滑铲表现状态。
    /// 只负责进入、循环和退出动画，不产生角色位移。
    /// </summary>
    public class PlayerSlideState : PlayerBaseState
    {
        private bool _isPlayingExit;

        public PlayerSlideState(NiumaCharacterController player) : base(player)
        {
        }

        /// <summary>
        /// 权威模拟仍处于 Slide 时禁止普通表现拦截。
        /// Slide 结束后重新开放 Jump、Roll、Dodge、Fall 等拦截器。
        /// </summary>
        protected override bool CheckInterrupts()
        {
            if (data.SimulationActionType == CharacterActionType.Slide)
            {
                return false;
            }

            return base.CheckInterrupts();
        }

        public override void Enter()
        {
            data.WantsToSlide = false;
            data.IsSliding = true;

            _isPlayingExit = false;

            PlayEnterOrLoop();
        }

        protected override void UpdateStateLogic()
        {
            if (data.SimulationActionType ==
                CharacterActionType.Slide)
            {
                /*
                 * 若退出动画尚未结束时又开始了新一轮滑铲，
                 * 重新播放进入动画。
                 */
                if (_isPlayingExit)
                {
                    _isPlayingExit = false;
                    PlayEnterOrLoop();
                }

                return;
            }

            /*
             * JumpInterceptor 通常会优先处理跳跃。
             * 这里处理远端快照或特殊时序下没有产生
             * 一次性 Jump 意图的兜底情况。
             */
            if (!data.IsGrounded)
            {
                AnimationFacade.ClearOnEndCallback();

                if (data.VerticalVelocity > 0f)
                {
                    data.WantsToJump = false;
                    data.NextStatePlayOptions =
                        config.LocomotionAnims.FadeInFallOptions;

                    player.StateMachine.ChangeState(
                        player.StateRegistry
                            .GetState<PlayerJumpState>());
                }
                else
                {
                    data.NextStatePlayOptions =
                        config.LocomotionAnims.FadeInFallOptions;

                    player.StateMachine.ChangeState(
                        player.StateRegistry
                            .GetState<PlayerFallState>());
                }

                return;
            }

            if (!_isPlayingExit)
            {
                TryFinishExitAtConfiguredTime();
                return;
            }

            PlayExitAnimation();
        }

        public override void PhysicsUpdate()
        {
            /*
             * 刻意留空。
             * CharacterSimulationRunner 已经完成滑铲位移，
             * 这里不能调用 MotionDriver，否则会产生第二份位移。
             */
        }

        public override void Exit()
        {
            AnimationFacade.ClearOnEndCallback();

            data.WantsToSlide = false;
            data.IsSliding = false;

            _isPlayingExit = false;
        }

        #region Animation Playback(动画播放)

        private void PlayEnterOrLoop()
        {
            AnimationFacade.ClearOnEndCallback();

            MotionClipData enterAnimation =
                config.Sliding.SlideEnterAnimation;

            if (HasClip(enterAnimation))
            {
                data.NextStatePlayOptions =
                    config.Sliding.SlideEnterOptions;

                ChooseOptionsAndPlay(enterAnimation.Clip);

                AnimationFacade.SetOnEndCallback(HandleEnterAnimationEnd);

                return;
            }

            PlayLoopAnimation();
        }

        private void HandleEnterAnimationEnd()
        {
            AnimationFacade.ClearOnEndCallback();

            if (data.SimulationActionType == CharacterActionType.Slide)
            {
                PlayLoopAnimation();
            }
            else
            {
                PlayExitAnimation();
            }
        }

        private void PlayLoopAnimation()
        {
            AnimationFacade.ClearOnEndCallback();

            MotionClipData loopAnimation =
                config.Sliding.SlideLoopAnimation;

            if (!HasClip(loopAnimation))
            {
                Debug.LogWarning("[滑铲表现] 没有配置 SlideLoopAnimation。", player);

                return;
            }

            ChooseOptionsAndPlay(loopAnimation.Clip);
        }

        private void PlayExitAnimation()
        {
            if (_isPlayingExit)
            {
                return;
            }

            _isPlayingExit = true;

            AnimationFacade.ClearOnEndCallback();

            MotionClipData exitAnimation = config.Sliding.SlideExitAnimation;

            if (!HasClip(exitAnimation))
            {
                ExitToLocomotionState();
                return;
            }

            /*
             * 当前 SlideSO 没有单独的退出动画淡入配置，
             * 因此沿用 ClipTransition 自身的 Fade 配置。
             */
            data.NextStatePlayOptions = AnimPlayOptions.Default;

            ChooseOptionsAndPlay(exitAnimation.Clip);

            AnimationFacade.SetOnEndCallback(ExitToLocomotionState);
        }

        private void TryFinishExitAtConfiguredTime()
        {
            MotionClipData exitAnimation = config.Sliding.SlideExitAnimation;

            if (AnimationFacade.CurrentTime < exitAnimation.EndTime)
            {
                return;
            }

            ExitToLocomotionState();

        }

        private static bool HasClip( MotionClipData clipData)
        {
            return clipData != null && clipData.Clip != null;
        }

        #endregion

        #region State Transition(状态切换)

        private void ExitToLocomotionState()
        {
            AnimationFacade.ClearOnEndCallback();

             /*
              * 不能只读取 CurrentLocomotionState。
              * 滑铲期间它可能仍保留进入前的 Sprint，
              * 必须结合当前移动输入判断是否真的还想移动。
              */
            bool hasMovementIntent = data.MoveInput.sqrMagnitude > 0.01f && data.CurrentLocomotionState != LocomotionState.Idle;

            if (!hasMovementIntent)
            {
                data.NextStatePlayOptions = config.Sliding.SlideToIdleOptions;

                player.StateMachine.ChangeState(player.StateRegistry.GetState<PlayerIdleState>());

                return;
            }

            data.NextStatePlayOptions = config.Sliding.SlideToMoveOptions;

            player.StateMachine.ChangeState(player.StateRegistry.GetState<PlayerMoveLoopState>());
        }

        #endregion

    }
}
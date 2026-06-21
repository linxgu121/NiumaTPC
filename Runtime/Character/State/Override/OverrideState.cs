using System;
using System.Collections;
using System.Collections.Generic;
using NiumaTPC.Character.Motion.MotionEnums;
using NiumaTPC.Character.State.Core;
using NiumaTPC.Character.State.Core.Locomotion;
using UnityEngine;

namespace NiumaTPC.Character.State.Override
{
    /// <summary>
    /// 处理强制全身动画播放的代理状态
    /// </summary>
    [Serializable]
    public sealed class OverrideState : PlayerBaseState
    {
        private bool _applied;

        public int CurrentPriority => data.Override.IsActive ? data.Override.Request.Priority : 0;

        public OverrideState(NiumaCharacterController player) : base(player) { }

        public override void Enter()
        {
            _applied = false;

            // 代理模式期间 拒绝物品栏切换
            data.Arbitration.BlockInventory = true;

            Apply();
        }

        public override void Exit()
        {
            // 清理 Override 专用回调通道 不影响其它系统绑定在 layer0 的回调
            AnimationFacade.ClearOverrideOnEndCallback();

            // 清理全身动作相关表现
            AnimationFacade.StopFullBodyAction();

            data.Override.Clear();
            data.Arbitration.BlockInventory = false;
        }

        protected override bool CheckInterrupts() => false;

        protected override void UpdateStateLogic()
        {
            if (!_applied) Apply();
        }

        public override void PhysicsUpdate()
        {
            if (!data.Override.IsActive) return;

            var req = data.Override.Request;
            if (req.MotionData != null)
            {
                player.MotionDriver.UpdateMotion(req.MotionData, AnimationFacade.CurrentTime, req.ApplyGravity);
                return;
            }

            if (req.ApplyGravity)
                player.MotionDriver.UpdateGravityOnly();
        }

        // 允许外部在不切换状态的情况下强制重播新请求
        public void ForceReapply()
        {
            _applied = false;
            Apply();
        }

        private void Apply()
        {
            if (!data.Override.IsActive) return;

            _applied = true;

            var req = data.Override.Request;

            AnimationFacade.PlayFullBodyAction(req.Clip, req.FadeDuration, req.MotionData == null);

            // 关键：全身Override的结束回调注册到 -1 通道 避免与物品/其他系统抢占 layer0 的 OnEnd 槽位
            AnimationFacade.SetOverrideOnEndCallback(OnClipEnd);
        }

        private void OnClipEnd()
        {
            AnimationFacade.ClearOverrideOnEndCallback();

            if (!data.Override.IsActive) return;

            if (data.Override.ReturnState != null)
            {
                player.StateMachine.ChangeState(data.Override.ReturnState);
                return;
            }

            if (data.CurrentLocomotionState != LocomotionState.Idle)
                player.StateMachine.ChangeState(player.StateRegistry.GetState<PlayerMoveLoopState>());
            else
                player.StateMachine.ChangeState(player.StateRegistry.GetState<PlayerIdleState>());
        }
    }
}

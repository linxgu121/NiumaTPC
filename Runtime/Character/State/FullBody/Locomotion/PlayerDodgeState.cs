using NiumaTPC.Character.Config;
using NiumaTPC.Character.Event;
using NiumaTPC.Character.Motion.MotionEnums;
using UnityEngine;

namespace NiumaTPC.Character.State.Core.Locomotion
{
    /// <summary>
    /// 玩家闪避状态 
    /// 负责执行闪避动画和独立平滑位移，根据移动方向选择八方向闪避，最后回到移动或空闲状态。
    /// </summary>
    public class PlayerDodgeState : PlayerBaseState
    {
        // 缓存当前选中的闪避数据
        private WarpedMotionData _selectedData;

        // 累计播放时长和是否已触发 EndTime 逻辑 防止重复执行
        private float _stateDuration;
        private bool _endTimeTriggered;
        private bool _animationEnded;

        public PlayerDodgeState(NiumaCharacterController player) : base(player) { }

        // 闪避状态不可被通用强制打断
        protected override bool CheckInterrupts() => false;

        // 进入状态 选择对应方向的闪避动画 初始化运动变形
        public override void Enter()
        {
            data.IsDodging = true;
            data.WantsToDodge = false;

            // 写入音频意图（由 AudioController 统一消费）
            data.SfxQueue.Enqueue(PlayerSfxEvent.Dodge);

            _stateDuration = 0f;
            _endTimeTriggered = false;
            _animationEnded = false;

            // 根据方向选择闪避数据
            _selectedData = GetDodgeData(out Vector3 actionLocalDirection);

            // 如果没有闪避数据 回到空闲
            if (_selectedData == null || _selectedData.Clip == null)
            {
                player.StateMachine.ChangeState(player.StateRegistry.GetState<PlayerIdleState>());
                return;
            }

            // 动画只负责表现，位移使用 DodgingSO 的独立配置。
            player.MotionDriver.InitializeActionMotion(
                actionLocalDirection,
                config.Dodging.DistanceMeters,
                config.Dodging.DurationSeconds,
                config.Dodging.ProgressCurve,
                config.Dodging.ApplyGravity);

            ChooseOptionsAndPlay(_selectedData.Clip);

            // 设置结束回调 如果提前触发EndTime则忽略此回调
            player.AnimationFacade.SetOnEndCallback(() =>
            {
                _animationEnded = true;
                if (_endTimeTriggered ||
                    !player.MotionDriver.IsActionMotionComplete)
                {
                    return;
                }

                HandleDodgeEnd();
            });

            // 记录末相位确保后续状态能正确选择脚位
            data.ExpectedFootPhase = _selectedData.EndPhase;
        }

        // 状态逻辑 闪避过程中一般不做任何中断检测
        protected override void UpdateStateLogic()
        {
        }

        // 物理更新 按 DodgingSO 的累计曲线驱动平滑位移
        public override void PhysicsUpdate()
        {
            if (_selectedData == null) return;

            bool motionComplete =
                player.MotionDriver.UpdateActionMotion(Time.deltaTime);

            // 累计播放时长 检查是否到达 EndTime 提前切换
            _stateDuration = player.AnimationFacade.CurrentTime;

            bool reachedEarlyEnd =
                _selectedData.EndTime > 0f &&
                _stateDuration >= _selectedData.EndTime;

            if (!_endTimeTriggered &&
                motionComplete &&
                (_animationEnded || reachedEarlyEnd))
            {
                _endTimeTriggered = true;
                HandleDodgeEnd();
                return;
            }
        }

        // 退出状态 清理Warp数据和回调
        public override void Exit()
        {
            data.IsDodging = false;
            data.WantsToDodge = false;

            player.MotionDriver.ClearActionMotion();

            player.AnimationFacade.ClearOnEndCallback();

            _selectedData = null;
        }

        // 根据运动方向获取闪避动画数据 8方向量化
        private WarpedMotionData GetDodgeData(
            out Vector3 localDirection)
        {
            float angle = data.DesiredLocalMoveAngle;

            const float SectorAngle = 45f;
            const float HalfSectorAngle = 22.5f;

            // 8方向判断逻辑 初始化使用连续输入的8方向扇区量化
            if (angle > -HalfSectorAngle && angle <= HalfSectorAngle)
            {
                localDirection = Vector3.forward;
                return config.Dodging.ForwardDodge;
            }

            if (angle > HalfSectorAngle && angle <= HalfSectorAngle + SectorAngle)
            {
                localDirection = new Vector3(1f, 0f, 1f).normalized;
                return config.Dodging.ForwardRightDodge;
            }

            if (angle > HalfSectorAngle + SectorAngle && angle <= HalfSectorAngle + SectorAngle * 2)
            {
                localDirection = Vector3.right;
                return config.Dodging.RightDodge;
            }

            if (angle > HalfSectorAngle + SectorAngle * 2 && angle <= 180f - HalfSectorAngle)
            {
                localDirection = new Vector3(1f, 0f, -1f).normalized;
                return config.Dodging.BackwardRightDodge;
            }

            if (angle > 180f - HalfSectorAngle || angle <= -180f + HalfSectorAngle)
            {
                localDirection = Vector3.back;
                return config.Dodging.BackwardDodge;
            }

            if (angle > -180f + HalfSectorAngle && angle <= -HalfSectorAngle - SectorAngle * 2)
            {
                localDirection = new Vector3(-1f, 0f, -1f).normalized;
                return config.Dodging.BackwardLeftDodge;
            }

            if (angle > -HalfSectorAngle - SectorAngle * 2 && angle <= -HalfSectorAngle - SectorAngle)
            {
                localDirection = Vector3.left;
                return config.Dodging.LeftDodge;
            }

            if (angle > -HalfSectorAngle - SectorAngle && angle <= -HalfSectorAngle)
            {
                localDirection = new Vector3(-1f, 0f, 1f).normalized;
                return config.Dodging.ForwardLeftDodge;
            }

            // 兜底使用左闪避
            localDirection = Vector3.left;
            return config.Dodging.LeftDodge;
        }

        // 处理闪避结束 根据当前运动状态切回MoveLoop或Idle
        private void HandleDodgeEnd()
        {
            _endTimeTriggered = true;

            // 如果闪避结束时处于空闲 就回到空闲状态 使用闪避的淡入选项
            if (data.CurrentLocomotionState == LocomotionState.Idle)
            {
                data.NextStatePlayOptions = config.Dodging.FadeInIdleOptions;
                player.StateMachine.ChangeState(player.StateRegistry.GetState<PlayerIdleState>());
            }
            else
            {
                // 否则回到运动循环 继承末相位供MoveLoop选择脚位
                data.NextStatePlayOptions = config.Dodging.FadeInMoveLoopOptions;
                data.ExpectedFootPhase = _selectedData.EndPhase;
                player.StateMachine.ChangeState(player.StateRegistry.GetState<PlayerMoveLoopState>());
            }
        }
    }
}

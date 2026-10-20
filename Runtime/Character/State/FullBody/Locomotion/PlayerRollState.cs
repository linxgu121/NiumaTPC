using NiumaTPC.Character.Config;
using NiumaTPC.Character.Event;
using NiumaTPC.Character.Motion.MotionEnums;
using NiumaTPC.Character.Simulation;

namespace NiumaTPC.Character.State.Core.Locomotion
{
    /// <summary>
    /// 玩家翻滚状态
    /// 翻滚表现状态，选择八方向动画，并在模拟动作结束后切回移动或空闲
    /// 不根据动画时间推进角色位移
    /// </summary>
    public class PlayerRollState : PlayerBaseState
    {
        //缓存当前选中的翻滚数据
        // 防止模拟动作结束后重复提交表现状态切换
        private WarpedMotionData _selectedData;

        private bool _endTimeTriggered;

        public PlayerRollState(NiumaCharacterController player) : base(player) { }

        // 翻滚状态不可被通用强制打断
        protected override bool CheckInterrupts() => false;

        // 进入状态 选择对应方向的翻滚动画 初始化运动变形
        public override void Enter()
        {
            data.WantsToRoll = false;

            // 写入音频意图（由 AudioController 统一消费）
            data.SfxQueue.Enqueue(PlayerSfxEvent.Roll);

            _endTimeTriggered = false;

            // 根据方向选择翻滚数据
            _selectedData = GetRollData();

            // 如果没有翻滚数据 回到空闲
            if (_selectedData == null || _selectedData.Clip == null)
            {
                player.StateMachine.ChangeState(player.StateRegistry.GetState<PlayerIdleState>());
                return;
            }

            ChooseOptionsAndPlay(_selectedData.Clip);

            // 记录末相位确保后续状态能正确选择脚位
            data.ExpectedFootPhase = _selectedData.EndPhase;
        }

        // 状态逻辑 翻滚过程中一般不做任何中断检测
        protected override void UpdateStateLogic()
        {
        }

        // 物理更新 计算运动变形时间 驱动Warp运动
        public override void PhysicsUpdate()
        {
            if (_selectedData == null || _endTimeTriggered)
            {
                return;
            }

            // 位移由固定 Tick 推进
            // 模拟动作结束后，表现层再选择后续动画
            if (data.SimulationActionType != CharacterActionType.Roll)
            {
                HandleRollEnd();
            }
        }

        // 退出状态 清理Warp数据和回调
        public override void Exit()
        {
            data.WantsToRoll = false;

            player.AnimationFacade.ClearOnEndCallback();

            _selectedData = null;
        }

        // 根据方向获取翻滚动画数据 8方向量化
        private WarpedMotionData GetRollData()
        {
            float angle = data.DesiredLocalMoveAngle;

            const float SectorAngle = 45f;
            const float HalfSectorAngle = 22.5f;

            // 8方向判断逻辑
            if (angle > -HalfSectorAngle && angle <= HalfSectorAngle)
                return config.Rolling.ForwardRoll;

            if (angle > HalfSectorAngle && angle <= HalfSectorAngle + SectorAngle)
                return config.Rolling.ForwardRightRoll;

            if (angle > HalfSectorAngle + SectorAngle && angle <= HalfSectorAngle + SectorAngle * 2)
                return config.Rolling.RightRoll;

            if (angle > HalfSectorAngle + SectorAngle * 2 && angle <= 180f - HalfSectorAngle)
                return config.Rolling.BackwardRightRoll;

            if (angle > 180f - HalfSectorAngle || angle <= -180f + HalfSectorAngle)
                return config.Rolling.BackwardRoll;

            if (angle > -180f + HalfSectorAngle && angle <= -HalfSectorAngle - SectorAngle * 2)
                return config.Rolling.BackwardLeftRoll;

            if (angle > -HalfSectorAngle - SectorAngle * 2 && angle <= -HalfSectorAngle - SectorAngle)
                return config.Rolling.LeftRoll;

            if (angle > -HalfSectorAngle - SectorAngle && angle <= -HalfSectorAngle)
                return config.Rolling.ForwardLeftRoll;

            // 兜底使用左翻滚
            return config.Rolling.LeftRoll;
        }

        /// <summary>
        /// 处理翻滚结束 
        /// 根据当前运动状态切回MoveLoop或Idle
        /// </summary>
        private void HandleRollEnd()
        {
            _endTimeTriggered = true;

            // 如果翻滚结束时处于空闲 就回到空闲状态 使用翻滚的淡入选项
            if (data.CurrentLocomotionState == LocomotionState.Idle)
            {
                data.NextStatePlayOptions = config.Rolling.FadeInIdleOptions;
                player.StateMachine.ChangeState(player.StateRegistry.GetState<PlayerIdleState>());
            }
            else
            {
                // 否则回到运动循环 继承末相位供MoveLoop选择脚位
                data.NextStatePlayOptions = config.Rolling.FadeInMoveLoopOptions;
                data.ExpectedFootPhase = _selectedData.EndPhase;
                player.StateMachine.ChangeState(player.StateRegistry.GetState<PlayerMoveLoopState>());
            }
        }
    }
}

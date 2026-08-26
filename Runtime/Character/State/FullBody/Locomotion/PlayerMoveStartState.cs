using System.Collections;
using System.Collections.Generic;
using NiumaTPC.Character.Config;
using NiumaTPC.Character.Core.Animation;
using NiumaTPC.Character.Motion.MotionEnums;
using NiumaTPC.Character.Simulation;
using NiumaTPC.Character.State.Core.Aiming;
using UnityEngine;

namespace NiumaTPC.Character.State.Core.Locomotion
{
    /// <summary>
    /// 玩家起步状态 
    /// 负责根据运动状态和移动方向选择8方向起步动画 并驱动角色初始移动 
    /// 动画结束后切换到循环移动状态
    /// </summary>
    public class PlayerMoveStartState : PlayerBaseState
    {
        public MotionClipData _currentClipData;
        public LocomotionState _startLocomotionState;
        private CharacterStartDirection _startDirection;

        public PlayerMoveStartState(NiumaCharacterController player) : base(player) { }

        // 进入状态 选择对应方向的起步动画并注册结束回调
        public override void Enter()
        {
            bool usesFixedTickSimulation =
                player.MotionDriver.IsExternalSimulationActive;

            _startLocomotionState = usesFixedTickSimulation
                ? data.SimulationStartLocomotionState
                : data.CurrentLocomotionState;

            _startDirection = usesFixedTickSimulation
                ? data.SimulationStartDirection
                : CharacterStartDirectionResolver.ResolveLocalAngle(
                    data.DesiredLocalMoveAngle);

            // 固定 Tick 模式下，动画和位移曲线必须使用同一方向、同一速度档位。
            PlayStartAnimation(_startDirection, _startLocomotionState);

            if(!usesFixedTickSimulation)
            {
                // End 回调 切换到 MoveLoop
                AnimationFacade.SetOnEndCallback(() =>
                {
                    // 应用自定义淡入时间
                    var nextOptions = data.CurrentLocomotionState switch
                    {
                        LocomotionState.Walk => config.LocomotionAnims.FadeInWalkLoopOptions,
                        LocomotionState.Jog => config.LocomotionAnims.FadeInRunLoopOptions,
                        LocomotionState.Sprint => config.LocomotionAnims.FadeInSprintLoopOptions,
                        _ => AnimPlayOptions.Default
                    };
                data.NextStatePlayOptions = nextOptions;

                player.StateMachine.ChangeState(player.StateRegistry.GetState<PlayerMoveLoopState>());
            });
            }
            
        }

         // 状态逻辑 检测瞄准 空闲等打断条件
        // 跳跃由全局拦截器统一处理，避免状态内重复判断
        protected override void UpdateStateLogic()
        {
            if (data.WantsToVault)
            {
                // 兜底处理：起步动画期间触发翻越时，直接切入翻越状态。
                data.NextStatePlayOptions = config.LocomotionAnims.FadeInVaultOptions;
                player.StateMachine.ChangeState(player.StateRegistry.GetState<PlayerVaultState>());
                return;
            }

            if(player.MotionDriver.IsExternalSimulationActive && data.SimulationMotionPhase == CharacterMotionPhase.Moving)
            {
                data.NextStatePlayOptions =
                   data.CurrentLocomotionState switch
                   {
                    LocomotionState.Walk => config.LocomotionAnims.FadeInWalkLoopOptions,

                    LocomotionState.Jog => config.LocomotionAnims.FadeInRunLoopOptions,

                    LocomotionState.Sprint => config.LocomotionAnims.FadeInSprintLoopOptions,

                    _ => AnimPlayOptions.Default
                     
                   };

                player.StateMachine.ChangeState(player.StateRegistry.GetState<PlayerMoveLoopState>());

                return;
            }

            if (data.IsAiming)
            {
                player.StateMachine.ChangeState(player.StateRegistry.GetState<PlayerAimMoveState>());
            }
            else if (data.CurrentLocomotionState == LocomotionState.Idle)
            {
                player.StateMachine.ChangeState(player.StateRegistry.GetState<PlayerIdleState>());
            }
            else if (player.MotionDriver.IsExternalSimulationActive &&
                     data.SimulationMotionPhase == CharacterMotionPhase.Starting &&
                     (data.SimulationStartDirection != _startDirection ||
                      data.SimulationStartLocomotionState != _startLocomotionState))
            {
                // 起步过程中突然反向时，模拟器会开启新一轮 Starting。
                // 表现层立即改播新的锁定方向，不能继续播放旧起步动画。
                _startDirection = data.SimulationStartDirection;
                _startLocomotionState =
                    data.SimulationStartLocomotionState;

                PlayStartAnimation(
                    _startDirection,
                    _startLocomotionState);
            }
            // 如果运动状态在起步中途改变 切到循环状态让其处理状态转换
            else if (data.CurrentLocomotionState != _startLocomotionState)
            {
                data.NextStatePlayOptions = config.LocomotionAnims.FadeInLoopBreakInOptions;
                player.StateMachine.ChangeState(player.StateRegistry.GetState<PlayerMoveLoopState>());
            }
        }

        // 物理更新 委托 MotionDriver 根据烘焙曲线驱动角色移动
        public override void PhysicsUpdate()
        {
            if (_currentClipData == null) return;

            float stateTime = AnimationFacade.CurrentTime;

            // 委托 将所有复杂的物理计算交给 MotionDriver
            player.MotionDriver.UpdateMotion(_currentClipData, stateTime);
        }

         // 退出状态 清理回调 中断曲线驱动 防止下一个起步瞬移
        public override void Exit()
        {
            AnimationFacade.ClearOnEndCallback();
            _currentClipData = null;

            // 清掉上一次的曲线增量旋转缓存 避免下次进入继承旧角度导致瞬回
            player.MotionDriver.InterruptClipDrivenMotion();

            float targetY = data.CurrentLocomotionState switch
            {
                LocomotionState.Walk => 0.35f,
                LocomotionState.Jog => 0.7f,
                LocomotionState.Sprint => 0.98f,
                _ => 0.7f
            };
            data.CurrentAnimBlendY = targetY;
        }

        private void PlayStartAnimation(
            CharacterStartDirection direction,
            LocomotionState locomotionState)
        {
            _currentClipData =
                SelectClipForLocomotionState(direction, locomotionState);

            if (_currentClipData == null || _currentClipData.Clip == null)
            {
                Debug.LogError(
                    $"[PlayerMoveStartState] 起步动画未配置：" +
                    $"Locomotion={locomotionState}, Direction={direction}。",
                    player);
                return;
            }

            ChooseOptionsAndPlay(_currentClipData.Clip);

            // 末相位用于后续循环与停止动画的左右脚选择。
            data.ExpectedFootPhase = _currentClipData.EndPhase;
        }

        // 根据固定 Tick 锁定的速度档位和八方向选择起步动画。
        private MotionClipData SelectClipForLocomotionState(
            CharacterStartDirection direction,
            LocomotionState locomotionState)
        {
            // 首先根据方向选择基础方向的动画
            MotionClipData walkClip = SelectDirectionClip(direction, isWalk: true);
            MotionClipData jogClip = SelectDirectionClip(direction, isWalk: false);
            MotionClipData sprintClip = SelectDirectionClip(direction, isSprint: true);

            // 然后根据运动状态返回对应的动画
            return locomotionState switch
            {
                LocomotionState.Walk => walkClip,
                LocomotionState.Jog => jogClip,
                LocomotionState.Sprint => sprintClip,
                _ => jogClip
            };
        }

        // 根据已经量化的八方向选择动画，不再重复计算角度边界。
        private MotionClipData SelectDirectionClip(
            CharacterStartDirection direction,
            bool isWalk = false,
            bool isSprint = false)
        {
            return direction switch
            {
                CharacterStartDirection.Forward =>
                    isWalk ? config.LocomotionAnims.WalkStartFwd :
                    isSprint ? config.LocomotionAnims.SprintStartFwd :
                    config.LocomotionAnims.RunStartFwd,

                CharacterStartDirection.ForwardRight =>
                    isWalk ? config.LocomotionAnims.WalkStartFwdRight :
                    isSprint ? config.LocomotionAnims.SprintStartFwdRight :
                    config.LocomotionAnims.RunStartFwdRight,

                CharacterStartDirection.Right =>
                    isWalk ? config.LocomotionAnims.WalkStartRight :
                    isSprint ? config.LocomotionAnims.SprintStartRight :
                    config.LocomotionAnims.RunStartRight,

                CharacterStartDirection.BackRight =>
                    isWalk ? config.LocomotionAnims.WalkStartBackRight :
                    isSprint ? config.LocomotionAnims.SprintStartBackRight :
                    config.LocomotionAnims.RunStartBackRight,

                CharacterStartDirection.Back =>
                    isWalk ? config.LocomotionAnims.WalkStartBack :
                    isSprint ? config.LocomotionAnims.SprintStartBack :
                    config.LocomotionAnims.RunStartBack,

                CharacterStartDirection.BackLeft =>
                    isWalk ? config.LocomotionAnims.WalkStartBackLeft :
                    isSprint ? config.LocomotionAnims.SprintStartBackLeft :
                    config.LocomotionAnims.RunStartBackLeft,

                CharacterStartDirection.Left =>
                    isWalk ? config.LocomotionAnims.WalkStartLeft :
                    isSprint ? config.LocomotionAnims.SprintStartLeft :
                    config.LocomotionAnims.RunStartLeft,

                CharacterStartDirection.ForwardLeft =>
                    isWalk ? config.LocomotionAnims.WalkStartFwdLeft :
                    isSprint ? config.LocomotionAnims.SprintStartFwdLeft :
                    config.LocomotionAnims.RunStartFwdLeft,

                _ => config.LocomotionAnims.RunStartFwd
            };
        }
    }

}

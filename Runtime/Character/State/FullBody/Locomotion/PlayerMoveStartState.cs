using NiumaTPC.Character.Config;
using NiumaTPC.Character.Core.Animation;
using NiumaTPC.Character.Motion.MotionEnums;
using NiumaTPC.Character.Simulation;
using NiumaTPC.Character.State.Core.Aiming;
using UnityEngine;

namespace NiumaTPC.Character.State.Core.Locomotion
{
    /// <summary>
    /// 玩家起步表现状态
    /// 根据模拟器给出的方向和速度档位播放起步动画
    /// 模拟进入持续移动阶段后切换到循环移动状态
    /// </summary>
    public class PlayerMoveStartState : PlayerBaseState
    {
        public MotionClipData _currentClipData;
        public LocomotionState _startLocomotionState;
        private CharacterStartDirection _startDirection;

        public PlayerMoveStartState(NiumaCharacterController player) : base(player) { }

        // 根据模拟器确定的起步方向和速度档位选择动画
        public override void Enter()
        {
            _startLocomotionState = data.SimulationStartLocomotionState;
            _startDirection = data.SimulationStartDirection;

            // 动画与位移模拟使用同一组起步数据
            PlayStartAnimation(_startDirection, _startLocomotionState);
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

            if (data.SimulationMotionPhase == CharacterMotionPhase.Moving)
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
            else if (data.SimulationMotionPhase == CharacterMotionPhase.Starting &&
            (data.SimulationStartDirection != _startDirection || data.SimulationStartLocomotionState != _startLocomotionState))
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

        // 位移由固定 Tick 模拟推进，表现状态不重复驱动角色
        public override void PhysicsUpdate()
        {
        }

        // 退出时清理动画缓存，并为后续循环动画设置混合值
        public override void Exit()
        {
            AnimationFacade.ClearOnEndCallback();
            _currentClipData = null;

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

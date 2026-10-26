using UnityEngine;

namespace NiumaTPC.Character.Simulation
{
    /// <summary>
    /// 负责判断 Roll/Dodge 能否启动，并锁定动作方向。
    /// 不负责推进位移或结束动作。
    /// </summary>
    public static class CharacterActionResolver
    {
        #region Public API

        /// <summary>
        /// 尝试根据当前 Tick 输入启动动作
        /// </summary>
        public static bool TryStartAction(
            ref CharacterSimulationState state,
            in CharacterInputCommand command,
            in CharacterSimulationConfig config)
        {
            if(state.ActionType != CharacterActionType.None)
            {
                return false;
            }

            // 同 Tick 同时出现 Jump 与 Roll/Dodge 时，
            // Jump/Vault 拥有更高优先级。
            if (command.HasButton(CharacterInputButtons.Jump))
            {
                return false;
            }

            if(!state.IsGrounded)
            {
                return false;
            }

            CharacterActionType requestedAction = ResolveRequestedAction(in command, in config);

            if(requestedAction == CharacterActionType.None)
            {
                return false;
            }


            state.ActionType = requestedAction;
            state.ActionTick = 0u;
            state.ActionDirection = ResolveActionDirection(state.Yaw, in command);

            /*
             * 普通移动的平滑缓存不能跨越动作保留。
             * 否则动作结束后可能立即恢复动作前的高速，
             * 破坏 ProgressCurve 的平滑收尾。
             */
            state.SmoothSpeed = 0f;
            state.SpeedSmoothVelocity = 0f;
            state.RotationSmoothVelocity = 0f;

            /*
             * 动作不能结束后继续执行动作前尚未完成的起步曲线。
             * 动作结束后由新输入重新决定 Starting 或 Idle。
             */
            state.MotionPhase = CharacterMotionPhase.Idle;
            state.MotionPhaseTick = 0u;

            return true;
        }

        #endregion

        #region Action Selection(动作选择)
        /// <summary>
        /// 保持PlayerBrain 的优先级：Roll 高于 Dodge。
        /// Profile 无效时认为该动作不可执行。
        /// </summary>
        private static CharacterActionType  ResolveRequestedAction(
            in CharacterInputCommand command,
            in CharacterSimulationConfig config)
        {
            bool wantsRoll = command.HasButton(CharacterInputButtons.Roll);

            if (wantsRoll && config.TryGetActionMotionProfile(
                    CharacterActionType.Roll,
                    out _))
            {
                return CharacterActionType.Roll;
            }

            bool wantsDodge = command.HasButton(CharacterInputButtons.Dodge);

            if (wantsDodge &&config.TryGetActionMotionProfile(
                    CharacterActionType.Dodge,
                    out _))
            {
                return CharacterActionType.Dodge;
            }

            return CharacterActionType.None;
        }

        #endregion

        #region Direction Lock(方向锁定)

        private static CharacterActionDirection ResolveActionDirection(
            float characterYaw,
            in CharacterInputCommand command)
        {
            bool hasMoveInput =
                CharacterMovementDirectionResolver.TryResolve(
                    in command,
                    out Vector3 worldDirection,
                    out _);

            // 与旧状态机一致：没有移动输入时默认向前。
            if (!hasMoveInput)
            {
                return CharacterActionDirection.Forward;
            }

            CharacterStartDirection direction =
                CharacterStartDirectionResolver.Resolve(
                    characterYaw,
                    in worldDirection);

            /*
             * 起步与动作使用相同的八方向扇区，
             * 但保持两个枚举契约独立，不直接强制转换数值。
             */
            return direction switch
            {
                CharacterStartDirection.Forward =>
                    CharacterActionDirection.Forward,

                CharacterStartDirection.ForwardRight =>
                    CharacterActionDirection.ForwardRight,

                CharacterStartDirection.Right =>
                    CharacterActionDirection.Right,

                CharacterStartDirection.BackRight =>
                    CharacterActionDirection.BackRight,

                CharacterStartDirection.Back =>
                    CharacterActionDirection.Back,

                CharacterStartDirection.BackLeft =>
                    CharacterActionDirection.BackLeft,

                CharacterStartDirection.Left =>
                    CharacterActionDirection.Left,

                CharacterStartDirection.ForwardLeft =>
                    CharacterActionDirection.ForwardLeft,

                _ => CharacterActionDirection.Forward
            };
        }

        #endregion
    }
}

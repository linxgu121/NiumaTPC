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

            if(!state.IsGrounded)
            {
                return false;
            }

            CharacterActionType requestedAction = ResolveRequestedAction(in command, in config);
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
    }
}

using NiumaTPC.Character.Motion.MotionEnums;

namespace NiumaTPC.Character.Simulation
{
    /// <summary>
    /// 根据某个 Tick 的输入命令推导角色运动档位。
    /// 不读取键盘、PlayerSO、Time 或场景组件。
    /// </summary>
    public static class CharacterLocomotionResolver
    {
        #region Constants(常量)
        
        /// <summary>
        /// 与现有 LocomotionIntentProcessor 保持一致。
        /// 因为比较的是 sqrMagnitude，所以实际输入长度阈值为 0.1。
        /// </summary>
        private const float MoveInputThresholdSquared = 0.01f;

        #endregion

        #region Public API

        public static LocomotionState Resolve(in CharacterInputCommand command, bool canSprint)
        {
            //判断输入向量长度小于等于死区阈值
            if (command.Move.sqrMagnitude <= MoveInputThresholdSquared)
            {
                return LocomotionState.Idle;
            }

            if (canSprint &&
                command.HasButton(CharacterInputButtons.Sprint))
            {
                return LocomotionState.Sprint;
            }

            if (command.HasButton(CharacterInputButtons.Walk))
            {
                return LocomotionState.Walk;
            }

            return LocomotionState.Jog;
        }

        #endregion

    }
}

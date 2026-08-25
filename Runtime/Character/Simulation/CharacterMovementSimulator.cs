using UnityEngine;

namespace NiumaTPC.Character.Simulation
{
    /// <summary>
    /// 编排单个 Tick 的基础角色移动模拟。
    /// 组合水平移动和垂直重力，但不直接操作场景组件。
    /// </summary>
    public static class CharacterMovementSimulator
    {
        #region Public API

        public static Vector3 Simulate(
            ref CharacterSimulationState state,
            in CharacterInputCommand command,
            in CharacterSimulationConfig config,
            bool canSprint,
            float tickDeltaTime
        )
        {
            state.Tick = command.Tick;

            Vector3 horizontalDisplacement = CharacterHorizontalMovementSimulator.Simulate(ref state, in command, in config, canSprint, tickDeltaTime);

            Vector3 verticalDisplacement = CharacterVerticalMovementSimulator.Simulate(ref state, in config, tickDeltaTime);

            return horizontalDisplacement + verticalDisplacement;
        }

        #endregion
    }
}

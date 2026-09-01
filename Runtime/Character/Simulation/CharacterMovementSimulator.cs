using UnityEngine;
using NiumaTPC.Character.Traversal;

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
            ICharacterTraversalProbe traversalProbe,
            bool canSprint,
            bool isHandsEmpty,
            float tickDeltaTime
        )
        {
            
            state.Tick = command.Tick;

            bool simulatedVault = CharacterVaultMovementSimulator.TrySimulate(
                    ref state,
                    in command,
                    in config,
                    traversalProbe,
                    out Vector3 vaultDisplacement);

            if (simulatedVault)
            {
                return vaultDisplacement;
            }

           bool simulatedAction = CharacterActionMovementSimulator.TrySimulate(
                    ref state,
                    in command,
                    in config,
                    out Vector3 actionDisplacement,
                    out bool actionAppliesGravity);

            Vector3 horizontalDisplacement =
                simulatedAction
                    ? actionDisplacement
                    : CharacterHorizontalMovementSimulator.Simulate(
                        ref state,
                        in command,
                        in config,
                        canSprint,
                        tickDeltaTime);

            Vector3 verticalDisplacement = Vector3.zero;

            /*
             * 普通移动始终执行垂直模拟。
             * 动作期间则遵循 RollSO/DodgingSO 的 ApplyGravity。
             */
            bool shouldSimulateVertical = !simulatedAction || actionAppliesGravity;

            if (shouldSimulateVertical)
            {
                verticalDisplacement =
                    CharacterVerticalMovementSimulator.Simulate(
                        ref state,
                        in command,
                        in config,
                        isHandsEmpty,
                        allowJumpInput: !simulatedAction,
                        tickDeltaTime);
            }

            return horizontalDisplacement + verticalDisplacement;

        }

        #endregion
    }
}

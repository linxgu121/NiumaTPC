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

            CharacterAimStateResolver.Update(ref state,in command,in config);
            
            // Jump/Vault 优先于 Roll、Dodge 和 Slide。
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


           bool simulatedAction;
           bool actionAppliesGravity;
           bool forceJumpInput = false;

           Vector3 actionDisplacement;

           if(state.ActionType == CharacterActionType.Slide)
            {
                // 已经处于滑铲时，不能交给 Roll/Dodge 模拟器处理。
                simulatedAction = CharacterSlideMovementSimulator.TrySimulate(
                    ref state,
                    in command,
                    in config,
                    tickDeltaTime,
                    out actionDisplacement,
                    out actionAppliesGravity,
                    out forceJumpInput);
            }
            else
            {
                /*
                 * 记录 Tick 开始时是否没有动作。
                 * Roll/Dodge 如果在本 Tick 刚结束，
                 * 不允许紧接着又在同 Tick 启动 Slide。
                 */
                bool mayStartSlide = state.ActionType == CharacterActionType.None;

                simulatedAction = CharacterActionMovementSimulator.TrySimulate(
                    ref state,
                    in command,
                    in config,
                    out actionDisplacement,
                    out actionAppliesGravity);

                if (!simulatedAction && mayStartSlide && state.ActionType == CharacterActionType.None)
                {
                    // Roll > Dodge > Slide。
                    simulatedAction =CharacterSlideMovementSimulator.TrySimulate(
                        ref state,
                        in command,
                        in config,
                        tickDeltaTime,
                        out actionDisplacement,
                        out actionAppliesGravity,
                        out forceJumpInput);
                }
            }

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
             * 普通移动始终执行垂直模拟
             * 动作期间则遵循 RollSO/DodgingSO 的 ApplyGravity
             * 滑铲缓存 Jump 拥有更高优先级，必须执行垂直模拟
             */
            bool shouldSimulateVertical = forceJumpInput || !simulatedAction || actionAppliesGravity;

            if (shouldSimulateVertical)
            {
                verticalDisplacement =
                    CharacterVerticalMovementSimulator.Simulate(
                        ref state,
                        in command,
                        in config,
                        isHandsEmpty,
                        allowJumpInput: !simulatedAction,
                        forceJumpInput: forceJumpInput,
                        tickDeltaTime);
            }

            return horizontalDisplacement + verticalDisplacement;

        }

        #endregion
    }
}

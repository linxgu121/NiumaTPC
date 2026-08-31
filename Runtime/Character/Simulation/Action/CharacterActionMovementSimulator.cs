using UnityEngine;

namespace NiumaTPC.Character.Simulation
{
    /// <summary>
    /// 按固定 Tick 推进 Roll/Dodge 权威位移。
    /// 动作期间普通水平移动输入不会改变已锁定路径。
    /// </summary>
    public static class CharacterActionMovementSimulator
    {
        //归一化
        private const float DiagonalComponent = 0.70710678f;

        #region Publc API

        public static bool TrySimulate(
            ref CharacterSimulationState state,
            in CharacterInputCommand command,
            in CharacterSimulationConfig config,
            out Vector3 horizontalDisplacement,
            out bool applyGravity)
        {
            horizontalDisplacement = Vector3.zero;
            applyGravity = true;

            if(state.ActionType == CharacterActionType.None)
            {
                bool started = CharacterActionResolver.TryStartAction(
                    ref state,
                    in command,
                    in config);

                if(!started)
                {
                    return false;
                }
            }

            if(!config.TryGetActionMotionProfile(
                state.ActionType,
                out CharacterActionMotionProfile profile))
            {
                FinishAction(ref state);
                return false;
            }

            /*
             * ActionTick 表示已经执行完成的 Tick 数。
             * 达到 DurationTicks 后，本 Tick 恢复普通移动。
             */
            if (state.ActionTick >= profile.DurationTicks)
            {
                FinishAction(ref state);
                return false;
            }

            if (!profile.TryGetDistance(state.ActionTick,out float distance))
            {
                FinishAction(ref state);
                return false;
            }

            Vector3 localDirection = GetLocalDirection(state.ActionDirection);

            Vector3 worldDirection = Quaternion.Euler(
                0f, 
                state.Yaw, 0f) * localDirection;

            worldDirection.y = 0f;

            if (worldDirection.sqrMagnitude > 0.0001f)
            {
                worldDirection.Normalize();
            }
            else
            {
                worldDirection = Vector3.forward;
            }

            horizontalDisplacement = worldDirection * distance;

            state.LastMoveDirection = worldDirection;
            state.ActionTick++;
            applyGravity = profile.ApplyGravity;

            return true;
        }

        #endregion

        #region Direction(方向)

        private static Vector3 GetLocalDirection(CharacterActionDirection direction)
        {
            return direction switch
            {
                CharacterActionDirection.Forward => Vector3.forward,

                CharacterActionDirection.ForwardRight => new Vector3(DiagonalComponent,0f,DiagonalComponent),

                CharacterActionDirection.Right => Vector3.right,

                CharacterActionDirection.BackRight => new Vector3(DiagonalComponent,0,-DiagonalComponent),

                CharacterActionDirection.Back => Vector3.back,

                CharacterActionDirection.BackLeft => new Vector3(-DiagonalComponent,0f,-DiagonalComponent),

                CharacterActionDirection.Left => Vector3.left,

                CharacterActionDirection.ForwardLeft => new Vector3(-DiagonalComponent, 0f, DiagonalComponent),

                _ => Vector3.forward


            };
        }

        #endregion

        #region Lifecycle(生命周期)

        private static void FinishAction(ref CharacterSimulationState state)
        {
            state.ActionType = CharacterActionType.None;
            state.ActionTick = 0u;
            state.ActionDirection = CharacterActionDirection.Forward;
        }

        #endregion

    }
}

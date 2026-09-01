using NiumaTPC.Character.RuntimeData;
using NiumaTPC.Character.Traversal;
using UnityEngine;

namespace NiumaTPC.Character.Simulation
{
    /// <summary>
    /// 启动并推进固定 Tick 翻越轨迹。
    /// 环境检测结果来自 ICharacterTraversalProbe，
    /// 本类不直接调用 Physics。
    /// </summary>
    public static class CharacterVaultMovementSimulator
    {
        #region Public API

        public static bool TrySimulate(
            ref CharacterSimulationState state,
            in CharacterInputCommand command,
            in CharacterSimulationConfig config,
            ICharacterTraversalProbe traversalProbe,
            out Vector3 displacement)
        {
            displacement = Vector3.zero;

            if (state.VaultType == VaultType.None)
            {
                if (!TryStartVault(
                        ref state,
                        in command,
                        in config,
                        traversalProbe))
                {
                    return false;
                }
            }

            return TryAdvanceVault(
                ref state,
                in config,
                out displacement);
        }

        #endregion

        #region Start(开始)

        private static bool TryStartVault(
            ref CharacterSimulationState state,
            in CharacterInputCommand command,
            in CharacterSimulationConfig config,
            ICharacterTraversalProbe traversalProbe)
        {
            bool canTryVault =
                command.HasButton(
                    CharacterInputButtons.Jump) &&
                state.IsGrounded &&
                state.ActionType ==
                    CharacterActionType.None;

            if (!canTryVault)
            {
                return false;
            }

            Vector3 forward =
                Quaternion.Euler(
                    0f,
                    state.Yaw,
                    0f) *
                Vector3.forward;

            if (TryProbeType(
                    VaultType.Low,
                    state.Position,
                    forward,
                    in config,
                    traversalProbe,
                    out VaultObstacleInfo lowInfo))
            {
                BeginVault(
                    ref state,
                    VaultType.Low,
                    in lowInfo);

                return true;
            }

            if (TryProbeType(
                    VaultType.High,
                    state.Position,
                    forward,
                    in config,
                    traversalProbe,
                    out VaultObstacleInfo highInfo))
            {
                BeginVault(
                    ref state,
                    VaultType.High,
                    in highInfo);

                return true;
            }

            return false;
        }

        private static bool TryProbeType(
            VaultType vaultType,
            Vector3 position,
            Vector3 forward,
            in CharacterSimulationConfig config,
            ICharacterTraversalProbe traversalProbe,
            out VaultObstacleInfo obstacleInfo)
        {
            obstacleInfo = default;

            // 没有有效轨迹时不能启动该类型翻越。
            if (!config.TryGetVaultMotionProfile(
                    vaultType,
                    out _))
            {
                return false;
            }

            float minHeight;
            float maxHeight;

            switch (vaultType)
            {
                case VaultType.Low:
                    minHeight = config.LowVaultMinHeight;
                    maxHeight = config.LowVaultMaxHeight;
                    break;

                case VaultType.High:
                    minHeight = config.HighVaultMinHeight;
                    maxHeight = config.HighVaultMaxHeight;
                    break;

                default:
                    return false;
            }

            var request = new VaultProbeRequest(
                position,
                forward,
                minHeight,
                maxHeight);

            return traversalProbe.TryProbeVault(
                in request,
                out obstacleInfo);
        }

        private static void BeginVault(
            ref CharacterSimulationState state,
            VaultType vaultType,
            in VaultObstacleInfo obstacleInfo)
        {
            Vector3 vaultForward =
                -obstacleInfo.WallNormal;

            vaultForward.y = 0f;

            if (vaultForward.sqrMagnitude < 0.0001f)
            {
                vaultForward =
                    Quaternion.Euler(
                        0f,
                        state.Yaw,
                        0f) *
                    Vector3.forward;
            }

            vaultForward.Normalize();

            float targetYaw = Mathf.Repeat(
                Mathf.Atan2(
                    vaultForward.x,
                    vaultForward.z) *
                Mathf.Rad2Deg,
                360f);

            state.VaultType = vaultType;
            state.VaultTick = 0u;
            state.VaultStartPosition = state.Position;
            state.VaultStartYaw = state.Yaw;
            state.VaultWallNormal =
                obstacleInfo.WallNormal;
            state.VaultLedgePoint =
                obstacleInfo.LedgePoint;
            state.VaultLandPoint =
                obstacleInfo.ExpectedLandPoint;
            state.VaultTargetYaw = targetYaw;

            // 翻越轨迹完整接管三维位移，
            // 不能继续叠加普通移动与重力。
            state.VerticalVelocity = 0f;
            state.SmoothSpeed = 0f;
            state.SpeedSmoothVelocity = 0f;
            state.RotationSmoothVelocity = 0f;
            state.LocomotionState =
                Motion.MotionEnums.LocomotionState.Idle;
            state.MotionPhase =
                CharacterMotionPhase.Idle;
            state.MotionPhaseTick = 0u;
            state.LastMoveDirection = vaultForward;
        }

        #endregion

        #region Advance(推进动作仿真一帧)

        private static bool TryAdvanceVault(
            ref CharacterSimulationState state,
            in CharacterSimulationConfig config,
            out Vector3 displacement)
        {
            displacement = Vector3.zero;

            if (!config.TryGetVaultMotionProfile(
                    state.VaultType,
                    out CharacterVaultMotionProfile profile))
            {
                FinishVault(ref state);
                return false;
            }

            if (state.VaultTick >= profile.DurationTicks ||
                !profile.TryGetProgress(
                    state.VaultTick,
                    out float firstStageProgress,
                    out float secondStageProgress,
                    out float rotationProgress))
            {
                FinishVault(ref state);
                return false;
            }

            Vector3 desiredPosition;

            if (secondStageProgress > 0f)
            {
                desiredPosition = Vector3.Lerp(
                    state.VaultLedgePoint,
                    state.VaultLandPoint,
                    secondStageProgress);
            }
            else
            {
                desiredPosition = Vector3.Lerp(
                    state.VaultStartPosition,
                    state.VaultLedgePoint,
                    firstStageProgress);
            }

            // 使用当前真实位置计算本 Tick 请求位移。
            // CharacterController 碰撞后的位置会由 Runner 写回状态。
            displacement =
                desiredPosition - state.Position;

            state.Yaw = Mathf.LerpAngle(
                state.VaultStartYaw,
                state.VaultTargetYaw,
                rotationProgress);

            state.VaultTick++;

            if (state.VaultTick >= profile.DurationTicks)
            {
                FinishVault(ref state);
            }

            return true;
        }

        #endregion

        #region Lifecycle(生命周期)

        private static void FinishVault(
            ref CharacterSimulationState state)
        {
            state.VaultType = VaultType.None;
            state.VaultTick = 0u;
        }

        #endregion
    }
}
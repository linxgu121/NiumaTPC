using NiumaTPC.Character.RuntimeData;
using UnityEngine;

namespace NiumaTPC.Character.Simulation
{
    /// <summary>
    /// 将固定 Tick 翻越状态转换成旧状态机能够消费的表现数据。
    /// 这里只生成动画与 IK 所需事实，不负责角色位移。
    /// </summary>
    public static class CharacterVaultPresentationBridge
    {
        #region Public API

        public static void Apply(
            PlayerRuntimeData data,
            VaultType nextVaultType,
            uint nextVaultTick,
            Vector3 nextWallNormal,
            Vector3 nextLedgePoint)
        {
            if (data == null)
            {
                return;
            }

            bool vaultStarted =
                nextVaultType != VaultType.None &&
                data.SimulationVaultType != nextVaultType;

            data.SimulationVaultType = nextVaultType;
            data.SimulationVaultTick = nextVaultTick;

            /*
             * 结束快照不清空目标，避免 PlayerVaultState 退出前
             * 双手 IK 在最后一帧跳向世界原点。
             */
            if (nextVaultType != VaultType.None)
            {
                data.SimulationVaultWallNormal = nextWallNormal;
                data.SimulationVaultLedgePoint = nextLedgePoint;
            }

            if (!vaultStarted)
            {
                return;
            }

            data.WantsToVault = true;
            data.WantsLowVault = nextVaultType == VaultType.Low;
            data.WantsHighVault = nextVaultType == VaultType.High;
        }

        #endregion
    }
}

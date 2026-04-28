using NiumaTPC.Character.State.Base;
using NiumaTPC.Character.State.Core.Locomotion;
using UnityEngine;

namespace  NiumaTPC.Character.State.Core.Interceptors
{
    /// <summary>
    /// 翻越全局拦截器
    /// 负责检测翻越意图 当玩家按下跳跃键触发翻越条件时自动切换到翻越状态
    /// </summary>
    [CreateAssetMenu(fileName = "VaultInterceptor", menuName = "NiumaTPC/Player/Interceptors/Vault")]
    public class VaultInterceptorSO : StateInterceptorSO
    {
        public override bool TryIntercept(NiumaCharacterController player, PlayerBaseState currentState, out PlayerBaseState nextState)
        {
            nextState = null;
            var data = player.RuntimeData;

            // 检测翻越意图 如果不在翻越状态 则切换到翻越状态
            if (data.WantsToVault && currentState is not PlayerVaultState)
            {
                nextState = player.StateRegistry.GetState<PlayerVaultState>();
                return true;
            }

            return false;
        }
    }
}

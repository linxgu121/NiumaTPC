using System;
using NiumaTPC.Character.Config;
using NiumaTPC.Character.Motion.MotionEnums;
using NiumaTPC.Character.RuntimeData;
using NiumaTPC.Character.Traversal;
using UnityEngine;

namespace NiumaTPC.Character.ProcessingPipeline.Intent
{
    /// <summary>
    /// 跳跃与翻越意图处理器
    /// </summary>
    public class JumpOrVaultIntentProcessor
    {
        private readonly PlayerRuntimeData _data;
        private readonly PlayerSO _config;
        private readonly Transform _playerTransform;
        private readonly ICharacterTraversalProbe _traversalProbe;

         public JumpOrVaultIntentProcessor(
            PlayerRuntimeData data,
            PlayerSO config,
            Transform playerTransform,
            ICharacterTraversalProbe traversalProbe)
        {
            _data = data ??
                throw new ArgumentNullException(nameof(data));

            _config = config ??
                throw new ArgumentNullException(nameof(config));

            _playerTransform = playerTransform != null
                ? playerTransform
                : throw new ArgumentNullException(
                    nameof(playerTransform));

            _traversalProbe = traversalProbe ??
                throw new ArgumentNullException(
                    nameof(traversalProbe));
        }
        
        // 只有当接收到明确的跳跃指令时，才瞬间进行物理环境扫描
        public bool Update(in ProcessedInputData input)
        {
            // 只在 Jump 按下边沿执行环境检测，
            // 不允许每帧持续发射翻越射线。
            if (!input.JumpPressed)
            {
                return false;
            }

            return HandleJumpIntent(_data);
        }

        // 跳跃意图处理与优先级仲裁
        private bool HandleJumpIntent(PlayerRuntimeData data)
        {
            // 低位翻越检测 (在这里才真正发射射线)
            if (TryGetVaultIntent(
                    out VaultObstacleInfo info,
                    _config.Vaulting.LowVaultMinHeight,
                    _config.Vaulting.LowVaultMaxHeight))
            {
                data.WantsToVault = true;
                data.WantsLowVault = true;
                data.CurrentVaultInfo = info;
                return true;
            }
            // 高位翻越检测
            if (TryGetVaultIntent(
                    out info,
                    _config.Vaulting.HighVaultMinHeight,
                    _config.Vaulting.HighVaultMaxHeight))
            {
                data.WantsToVault = true;
                data.WantsHighVault = true;
                data.CurrentVaultInfo = info;
                return true;
            }


            // 普通地面跳跃(在没有检测到障碍时)
            if (data.IsGrounded)
            {
                data.WantsToJump = true;
                return true;
            }

            // 空中二段跳
            if (!data.IsGrounded && !data.HasPerformedDoubleJumpInAir)
            {
                data.DoubleJumpDirection = DoubleJumpDirection.Up;
                data.WantsDoubleJump = true;
                return true;
            }

            return false;
        }

         private bool TryGetVaultIntent(
            out VaultObstacleInfo obstacleInfo,
            float minHeight,
            float maxHeight)
        {
            var request = new VaultProbeRequest(
                _playerTransform.position,
                _playerTransform.forward,
                minHeight,
                maxHeight);

            return _traversalProbe.TryProbeVault(
                in request,
                out obstacleInfo);
        }

    }
}

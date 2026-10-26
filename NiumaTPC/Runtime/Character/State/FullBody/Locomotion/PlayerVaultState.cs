using NiumaTPC.Character.Simulation;
using NiumaTPC.Character.Config;
using NiumaTPC.Character.Motion.MotionEnums;
using UnityEngine;

namespace NiumaTPC.Character.State.Core.Locomotion
{
    /// <summary>
    /// 玩家翻越状态 
    /// 翻越表现状态，播放对应动画并根据模拟结果重建手部 IK
    /// 位移和翻越结束由固定 Tick 模拟决定
    /// </summary>
    public class PlayerVaultState : PlayerBaseState
    {
        private WarpedMotionData _selectedWarpData;

        public PlayerVaultState(NiumaCharacterController player) : base(player) { }

        // 翻越状态不允许被通用强制打断 避免反复进入退出
        protected override bool CheckInterrupts() => false;

        // 进入状态 根据意图或高度选择翻越动画 初始化运动变形
        public override void Enter()
        {
            //Debug.Log("Entered Vault State");
            data.IsVaulting = true;

            // 根据明确的意图选择翻越动画
            if (data.WantsLowVault && config.Vaulting.lowVaultAnim != null)
            {
                _selectedWarpData = config.Vaulting.lowVaultAnim;
            }
            else if (data.WantsHighVault && config.Vaulting.highVaultAnim != null)
            {
                _selectedWarpData = config.Vaulting.highVaultAnim;
            }
            else
            {
                // 没有明确意图 根据高度自动选择
                Debug.LogWarning("No explicit vault intent, falling back to height-based selection.");
                if (data.CurrentVaultInfo.IsValid)
                {
                    float h = data.CurrentVaultInfo.Height;
                    if (h >= 0.5f && h < 1.2f && config.Vaulting.lowVaultAnim != null)
                        _selectedWarpData = config.Vaulting.lowVaultAnim;
                    else if (h >= 1.2f && h <= 2.5f && config.Vaulting.highVaultAnim != null)
                        _selectedWarpData = config.Vaulting.highVaultAnim;
                    else
                        _selectedWarpData = null;
                }
                else
                {
                    _selectedWarpData = null;
                }
            }

            // 清空一次性意图
            data.WantsLowVault = false;
            data.WantsHighVault = false;
            data.WantsToVault = false;

            // 如果没有选中动画 直接回到空闲
            if (_selectedWarpData == null || _selectedWarpData.Clip == null)
            {
                player.StateMachine.ChangeState(player.StateRegistry.GetState<PlayerIdleState>());
                return;
            }

            ChooseOptionsAndPlay(_selectedWarpData.Clip);

            // 固定 Tick 已负责翻越路径
            // 这里只根据模拟结果重建手部 IK，不初始化旧位移驱动
            InitializeExternalVaultIK();
        }

        // 状态逻辑 翻越过程中一般不做任何中断检测
        protected override void UpdateStateLogic()
        {
            // 表现层等待模拟翻越结束，不使用动画 EndTime 提前退出
            if (data.SimulationVaultType != VaultType.None)
            {
                return;
            }

            ExitToLocomotionState();
        }

        // 物理更新 计算运动变形时间 驱动Warp运动
        public override void PhysicsUpdate()
        {
            if (_selectedWarpData == null)
            {
                return;
            }

            // 动画时间只用于 IK 权重，不再用于角色位移
            data.NormalizedWarpTime = Mathf.Clamp01(AnimationFacade.CurrentNormalizedTime);
        }

        // 退出状态 清理Warp数据和回调
        public override void Exit()
        {
            AnimationFacade.ClearOnEndCallback();

            data.IsVaulting = false;
            data.IsWarping = false;
            data.ActiveWarpData = null;

            _selectedWarpData = null;
        }

        #region External Vault IK(外部翻越IK)

        /// <summary>
        /// 使用服务器确认的墙面法线和墙沿根节点目标，
        /// 在本地重建双手 IK。该方法不初始化 MotionDriver，
        /// 因而不会产生第二份角色位移。
        /// </summary>
        private void InitializeExternalVaultIK()
        {
            data.IsWarping = false;
            data.ActiveWarpData = null;

            Vector3 wallNormal =
                data.SimulationVaultWallNormal;

            wallNormal.y = 0f;

            if (wallNormal.sqrMagnitude < 0.0001f)
            {
                return;
            }

            wallNormal.Normalize();

            Vector3 vaultForward =
                -wallNormal;

            Quaternion vaultRotation =
                Quaternion.LookRotation(
                    vaultForward,
                    Vector3.up);

            CharacterController controller =
                player.CharacterController;

            /*
             * SimulationVaultLedgePoint 是角色根节点目标。
             * 先按权威目标朝向恢复胶囊脚底偏移，
             * 才能得到墙顶表面上的手部定位基准。
             */
            Vector3 localFootPoint =
                controller.center +
                Vector3.down *
                (controller.height * 0.5f);

            Vector3 worldFootOffset =
                controller.transform.TransformVector(
                    localFootPoint);

            Vector3 scaledLocalFootOffset =
                Quaternion.Inverse(
                    controller.transform.rotation) *
                worldFootOffset;

            Vector3 ledgeSurfacePoint =
                data.SimulationVaultLedgePoint +
                vaultRotation *
                scaledLocalFootOffset;

            Vector3 rightDirection =
                vaultRotation *
                Vector3.right;

            float halfHandSpread =
                config.Vaulting.VaultHandSpread *
                0.5f;

            data.WarpIKTarget_LeftHand =
                ledgeSurfacePoint -
                rightDirection * halfHandSpread +
                vaultRotation *
                config.Vaulting.LeftHandIKOffset;

            data.WarpIKTarget_RightHand =
                ledgeSurfacePoint +
                rightDirection * halfHandSpread +
                vaultRotation *
                config.Vaulting.RightHandIKOffset;

            data.WarpIKRotation_Hand =
                vaultRotation *
                Quaternion.Euler(
                    config.Vaulting
                        .HandRotationOffsetEuler);

            // IsWarping 在此只表示启用翻越手部 IK
            // ActiveWarpData 提供权重曲线，不代表启用旧位移驱动
            data.IsWarping = true;
            data.ActiveWarpData = _selectedWarpData;
            data.NormalizedWarpTime = 0f;
        }

        #endregion

        #region State Transition(状态切换)

        private void ExitToLocomotionState()
        {
            if (data.CurrentLocomotionState != LocomotionState.Idle)
            {
                data.NextStatePlayOptions = config.Vaulting.VaultToMoveOptions;

                player.StateMachine.ChangeState(
                    player.StateRegistry.GetState<PlayerMoveLoopState>());
            }
            else
            {
                data.NextStatePlayOptions = config.Vaulting.VaultToIdleOptions;

                player.StateMachine.ChangeState(player.StateRegistry.GetState<PlayerIdleState>());
            }
        }

        #endregion
    }
}

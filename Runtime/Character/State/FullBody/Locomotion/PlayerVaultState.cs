using NiumaTPC.Character.Simulation;
using NiumaTPC.Character.Config;
using NiumaTPC.Character.Core.Animation;
using NiumaTPC.Character.Motion.MotionEnums;
using NiumaTPC.Character.RuntimeData;
using UnityEngine;

namespace NiumaTPC.Character.State.Core.Locomotion
{
    /// <summary>
    /// 玩家翻越状态 
    /// 负责执行翻越障碍物的动画和运动变形 根据输入或高度自动选择低翻或高翻
    /// </summary>
    public class PlayerVaultState : PlayerBaseState
    {
        private float _stateDuration;
        private bool _endTimeTriggered;
        private WarpedMotionData _selectedWarpData;

        public PlayerVaultState(NiumaCharacterController player) : base(player) { }

        // 翻越状态不允许被通用强制打断 避免反复进入退出
        protected override bool CheckInterrupts() => false;

        // 进入状态 根据意图或高度选择翻越动画 初始化运动变形
        public override void Enter()
        {
            //Debug.Log("Entered Vault State");
            _stateDuration = 0f;
            data.IsVaulting = true;
            _endTimeTriggered = false;

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

            /*
             * 固定 Tick 已经负责完整翻越位移。
             * 这里仅播放动画，不能再初始化旧 Motion Warp，
             * 否则表现目标和权威位置会互相争夺。
             */
            if (player.MotionDriver.IsExternalSimulationActive)
            {
                InitializeExternalVaultIK();
                return;
            }

            // 以下目标数组只供未启用固定 Tick 的旧 Motion Warp 链路使用。
            VaultObstacleInfo info =
                data.CurrentVaultInfo;

            Vector3[] warpTargets =
            {
                info.LedgePoint,
                info.ExpectedLandPoint
            };

            // 初始化 Motion Warping
            player.MotionDriver.InitializeWarpData(_selectedWarpData, warpTargets);

            data.IsWarping = true;
            data.ActiveWarpData = _selectedWarpData;
            data.NormalizedWarpTime = 0f;

            // 注入手部IK目标
            data.WarpIKTarget_LeftHand = data.CurrentVaultInfo.LeftHandPos;
            data.WarpIKTarget_RightHand = data.CurrentVaultInfo.RightHandPos;
            data.WarpIKRotation_Hand = data.CurrentVaultInfo.HandRot;

            AnimationFacade.SetOnEndCallback(ExitToLocomotionState);
        }

        // 状态逻辑 翻越过程中一般不做任何中断检测
        protected override void UpdateStateLogic()
        {
            if (!player.MotionDriver.IsExternalSimulationActive)
            {
                return;
            }

            // 动画不能决定权威翻越何时结束。
            // 当固定 Tick 状态回到 None，表现状态才能退出。
            if (data.SimulationVaultType != VaultType.None)
            {
                return;
            }

            ExitToLocomotionState();
        }

        // 物理更新 计算运动变形时间 驱动Warp运动
        public override void PhysicsUpdate()
        {
            if (player.MotionDriver.IsExternalSimulationActive)
            {
                // IK 权重属于表现数据，可以跟随本地动画时间。
                data.NormalizedWarpTime =
                    Mathf.Clamp01(
                        AnimationFacade.CurrentNormalizedTime);

                return;
            }

            if (_selectedWarpData == null) return;

            float normalizedTime = Mathf.Clamp01(AnimationFacade.CurrentNormalizedTime);
            data.NormalizedWarpTime = normalizedTime;

            // 累计播放时长 用于 EndTime 检测
            _stateDuration = AnimationFacade.CurrentTime;

            // 检测是否可以提前切回运动循环
            if (!_endTimeTriggered && data.CurrentLocomotionState != LocomotionState.Idle &&
                _selectedWarpData.EndTime > 0f && _stateDuration >= _selectedWarpData.EndTime)
            {
                _endTimeTriggered = true;
                if (data.MoveInput.sqrMagnitude > 0.01f)
                {
                    data.NextStatePlayOptions = AnimPlayOptions.Default;
                    player.StateMachine.ChangeState(player.StateRegistry.GetState<PlayerMoveLoopState>());
                }
                else
                {
                    player.StateMachine.ChangeState(player.StateRegistry.GetState<PlayerIdleState>());
                }
                return;
            }

            // 驱动运动变形
            player.MotionDriver.UpdateWarpMotion(normalizedTime);
        }

        // 退出状态 清理Warp数据和回调
        public override void Exit()
        {
            AnimationFacade.ClearOnEndCallback();

            data.IsVaulting = false;
            data.IsWarping = false;
            data.ActiveWarpData = null;

            player.MotionDriver.ClearWarpData();
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

            /*
             * 外部模拟下 IsWarping 只启用既有 IKController。
             * MotionDriver.UpdateWarpMotion 已有外部模拟保护，
             * 不会再次移动 CharacterController。
             */
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

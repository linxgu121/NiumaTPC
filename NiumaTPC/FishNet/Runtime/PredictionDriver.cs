using UnityEngine;
using FishNet.Utility.Template;
using NiumaTPC.Character;
using NiumaTPC.Character.Simulation;
using NiumaTPC.FishNet.Prediction;
using NiumaTPC.Character.RuntimeData;
using NiumaTPC.Character.Motion.MotionEnums;
using FishNet.Object.Prediction;
using FishNet.Transporting;
using FishNet.Connection;
using FishNet.Object;
using NiumaTPC.Character.Traversal;
using NiumaTPC.Character.Combat;
using NiumaTPC.FishNet.Combat;
using NiumaTPC.Character.Input.AI;
using NiumaTPC.FishNet.Enemy;

namespace NiumaTPC.FishNet
{
    /// <summary>
    /// NiumaTPC 与 FishNet 预测系统之间的适配器
    ///
    /// 本类负责：
    /// 1. 接收 FishNet 固定 Tick
    /// 2. 管理角色移动控制权
    /// 3. 后续提交 Replicate 与 Reconcile
    ///
    /// 它不负责实现具体移动规则
    /// 具体移动仍由 CharacterSimulationRunner 执行
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NiumaCharacterController))]
    public sealed class NiumaFishNetPredictionDriver : TickNetworkBehaviour
    {
        #region Inspector(组件)

        [Header("NiumaTPC依赖")]
        [SerializeField]
        [Tooltip("当前网络角色的 NiumaCharacterController；为空时会自动获取同物体组件。")]
        private NiumaCharacterController _player;

        [SerializeField]
        [Tooltip("当前网络角色的近战选敌组件，未绑定时自动获取")]
        private MeleeTargetingController _meleeTargeting;

        [Header("服务器近战判定")]

        [SerializeField]
        [Tooltip("服务器近战命中处理器，在 Hurtbox Layers 中选择 DamageHurtbox Layer")]
        private ServerMeleeHitProcessor _serverMeleeHitProcessor = new ServerMeleeHitProcessor();

        [Header("观察者表现同步")]
        [SerializeField]
        [Min(1)]
        [Tooltip("服务器每隔多少个网络 Tick 发送一次表现快照,推荐值为 2:当 Tick Rate 为 60 时，相当于每秒发送 30 次。")]
        private int _presentationSendIntervalTicks = 2;

        [Header("诊断")]
        [Tooltip("启用后每秒打印一次 FishNet Tick 和网络身份。")]
        [SerializeField]
        private bool _logTickHeartbeat = true;

        [SerializeField]
        [Tooltip("启用后，观察客户端会在表现状态变化时打印快照,稳定状态下最多每秒打印一次。")]
        private bool _logPresentationSnapshots;

        #endregion

        #region Runtime State(运行时状态)

        private CharacterInputCommandBuilder _commandBuilder;

        private IMeleeAttackSource _meleeAttackSource;

        private ServerEnemyControlBridge _enemyControlBridge;

        private CharacterSimulationRunner _runner;

        // 每个角色独立记录已经处理过的落地表现
        private readonly CharacterLandingPresentationBridge _landingPresentation = new CharacterLandingPresentationBridge();

        // 只记录服务器正常推进后的结果
        // 客户端预测和校正重放不能覆盖它
        private CharacterSimulationState _serverSimulationState;
        private bool _hasServerSimulationState;

        /// <summary>
        /// FishNet 当前是否已经取得角色模拟控制权
        /// </summary>
        private bool _networkSimulationActive;

        /// <summary>
        /// 网络接管前，角色是否已经处于外部状态驱动模式
        /// 释放网络控制时需要恢复
        /// </summary>
        private bool _externalStateDrivenBeforeNetwork;
        private bool _networkAppliedExternalStateDrive;

        /// <summary>
        /// 网络层是否已经为该角色应用本地输入所有权规则
        /// </summary>
        private bool _inputOwnershipApplied;

        private bool _inputWasBlockedBeforeNetwork;
        private bool _networkBlockedInput;

        //远端观察者

        /// <summary>
        /// 纯观察客户端最后接受的服务器表现快照 Tick
        /// </summary>
        private uint _lastReceivedPresentationTick;

        /// <summary>
        ///  是否已经接受过第一份服务器表现快照。
        ///  第一份快照不能直接与默认 Tick 0 比较。
        /// </summary>
        private bool _hasReceivedPresentationSnapshot;

        //日志
        /// <summary>
        /// 表现快照诊断使用的上一次状态
        /// 只负责限制日志频率，不参与玩法和网络判断
        /// </summary>
        private CharacterPresentationState _lastPresentationDiagnosticSnapshot;

        //是否已经输出过一次表现诊断快照
        private bool _hasPresentationDiagnosticSnapshot;
        //下一次允许打印诊断日志的时间戳
        private float _nextPresentationDiagnosticTime;

        #endregion

        #region 服务器模拟状态查询

        /// <summary>
        /// 获取服务器最近完成的一次模拟结果
        /// 返回值副本，调用者不能借此修改模拟器
        /// </summary>
        public bool TryGetServerSimulationState(out CharacterSimulationState state)
        {
            state = default;

            if (!isActiveAndEnabled ||
                !IsServerInitialized ||
                !_networkSimulationActive ||
                _runner == null ||
                !_hasServerSimulationState)
            {
                return false;
            }

            state = _serverSimulationState;
            return true;
        }

        #endregion

        #region Unity Lifecycle(Unity生命周期)

        private void Awake()
        {
            if (_player == null)
            {
                _player = GetComponent<NiumaCharacterController>();
            }

            if (_meleeTargeting == null)
            {
                _meleeTargeting = GetComponent<MeleeTargetingController>();
            }

            _meleeAttackSource = GetComponent<IMeleeAttackSource>();
            _enemyControlBridge = GetComponent<ServerEnemyControlBridge>();

            if (_serverMeleeHitProcessor == null)
            {
                _serverMeleeHitProcessor = new ServerMeleeHitProcessor();
            }

            /*
             * Tick：
             * 客户端预测和服务器权威模拟将在这里执行。
             *
             * PostTick：
             * 当前 Tick 模拟结束后创建服务器校正状态。
             */
            SetTickCallbacks(TickCallback.Tick | TickCallback.PostTick);
        }

        #endregion

        #region FishNet Lifecycle(FishNet生命周期)

        public override void OnStartNetwork()
        {
            ResetPresentationSnapshotTracking();

            if (!TryTakeSimulationOwnership())
            {
                return;
            }

            ApplyInputOwnership();

            Debug.Log(
            $"[NiumaFishNet] 网络驱动已接管角色：" +
            $"ObjectId={ObjectId}, " +
            $"OwnerId={OwnerId}, " +
            $"IsOwner={Owner.IsLocalClient}, " +
            $"Server={IsServerInitialized}, " +
            $"Client={IsClientInitialized}",
            this);
        }

        public override void OnStopNetwork()
        {
            ReleaseInputOwnership();
            ReleaseSimulationOwnership();
            ResetPresentationSnapshotTracking();
        }

        public override void OnStartServer()
        {
            base.OnStartServer();

            _serverMeleeHitProcessor.Reset();

            _serverSimulationState = default;
            _hasServerSimulationState = false;

            /*
             * OnStartNetwork与OnStartServer的组件回调顺序不应影响AI输入判定
             * 服务器初始化完成后重新应用一次输入控制权
            */
            if (_networkSimulationActive)
            {
                ReleaseInputOwnership();
                ApplyInputOwnership();
            }
        }

        public override void OnStopServer()
        {
            _serverSimulationState = default;
            _hasServerSimulationState = false;

            base.OnStopServer();
        }

        public override void OnOwnershipClient(NetworkConnection prevOwner)
        {
            if (!_networkSimulationActive)
            {
                return;
            }

            // 所有权变化后，当前对象可能从 Owner 变成观察者
            // 或从观察者变成 Owner，因此重新开始快照序列
            ResetPresentationSnapshotTracking();

            ReleaseInputOwnership();
            ApplyInputOwnership();
        }


        #endregion

        #region Input Ownership(输入所有权)

        /// <summary>
        /// 根据本地Owner、服务器AI和观察者身份分配输入采集权
        /// 服务器AI只允许在固定Tick采样输入
        /// </summary>
        private void ApplyInputOwnership()
        {
            if (_player == null || _inputOwnershipApplied)
            {
                return;
            }

            _inputWasBlockedBeforeNetwork = _player.IsInputBlocked;
            _externalStateDrivenBeforeNetwork = _player.IsExternalSimulationStateDriven;

            bool hasLocalInputAuthority = Owner.IsLocalClient;
            bool hasServerEnemyInputAuthority = HasServerEnemyInputAuthority();

            /*
             * 本地玩家由渲染帧读取设备输入
             * 服务器AI由FishNet固定Tick读取EnemyInputSource
             * 纯观察者完全禁止产生输入
             */
            _networkBlockedInput = !hasLocalInputAuthority && !hasServerEnemyInputAuthority;
            _networkAppliedExternalStateDrive = !hasLocalInputAuthority;

            if (_networkBlockedInput)
            {
                _player.SetInputBlocked(true, clearBufferedInput: true);
            }

            if (_networkAppliedExternalStateDrive)
            {
                _player.SetExternalSimulationStateDriven(true, clearLocalIntent: false);
            }

            _inputOwnershipApplied = true;

            Debug.Log(
                $"[NiumaFishNet] 输入所有权：" +
                $"ObjectId={ObjectId}, " +
                $"LocalOwner={hasLocalInputAuthority}, " +
                $"ServerEnemy={hasServerEnemyInputAuthority}, " +
                $"InputBlocked={_player.IsInputBlocked}, " +
                $"ExternalStateDriven={_player.IsExternalSimulationStateDriven}",
                this);
        }

        private void ReleaseInputOwnership()
        {
            if (!_inputOwnershipApplied)
            {
                return;
            }

            if (_player != null)
            {
                if (_networkAppliedExternalStateDrive)
                {
                    _player.SetExternalSimulationStateDriven(_externalStateDrivenBeforeNetwork, clearLocalIntent: false);
                }

                if (_networkBlockedInput)
                {
                    // 只撤销网络层施加的阻断，保留网络接管前的输入状态
                    _player.SetInputBlocked(_inputWasBlockedBeforeNetwork, clearBufferedInput: false);
                }
            }

            _inputOwnershipApplied = false;
            _inputWasBlockedBeforeNetwork = false;
            _networkBlockedInput = false;
            _externalStateDrivenBeforeNetwork = false;
            _networkAppliedExternalStateDrive = false;
        }

        /// <summary>
        /// 检查当前对象是否为服务器控制且正确绑定EnemyInputSource的敌人
        /// 仅存在EnemyBrain但仍使用玩家输入源时不能获得AI输入权
        /// </summary>
        private bool HasServerEnemyInputAuthority()
        {
            return IsServerInitialized &&
                   _enemyControlBridge != null &&
                   _player != null &&
                   _player.InputSourceRef is EnemyInputSource;
        }

        #endregion

        #region FishNet Tick(FishNet固定Tick)

        protected override void TimeManager_OnTick()
        {
            if (!_networkSimulationActive)
            {
                return;
            }

            PrintTickHeartbeat();

            /*
             * 玩家Owner读取渲染帧已经处理好的设备输入
             * 服务器敌人在当前固定Tick生成并采样AI输入
             * 两者最终都进入同一个Replicate与CharacterSimulationRunner
             */
            bool preparedServerEnemyInput = TryPrepareServerEnemyInput();
            ReplicateData data = BuildReplicateData(preparedServerEnemyInput);

            PerformReplicate(data);
        }

        protected override void TimeManager_OnPostTick()
        {
            if (!_networkSimulationActive)
            {
                return;
            }

            /*
             * Reconcile 负责修正拥有者的预测模拟
             * PresentationState 负责告诉观察者应该播放什么表现
             * 两者都是服务器 Tick 完成后的结果，但用途不同
             */
            CreateReconcile();
            TrySendPresentationState();
        }
        #endregion

        #region Input Construction(输入构建)

        /// <summary>
        /// 检查服务器敌人输入权并在当前固定Tick生成、采样AI输入
        /// 纯客户端和玩家对象不能进入该流程
        /// </summary>
        private bool TryPrepareServerEnemyInput()
        {
            if (!HasServerEnemyInputAuthority() ||
                !_enemyControlBridge.IsServerControlActive ||
                _player.InputPipeline == null)
            {
                return false;
            }

            uint serverTick = TimeManager.LocalTick;
            float tickDeltaTime = (float)TimeManager.TickDelta;

            if (!_enemyControlBridge.TryRunServerTick(serverTick, tickDeltaTime))
            {
                return false;
            }

            /*
             * 敌人的帧级Update已被ExternalSimulationStateDriven关闭
             * 因此这里只有一次输入采样，不会和渲染帧重复执行
             */
            _player.InputPipeline.Update();
            return true;
        }

        /// <summary>
        /// 为本地Owner或服务器权威敌人构建当前Tick的模拟输入
        /// 观察客户端不能自行构造角色命令
        /// </summary>
        private ReplicateData BuildReplicateData(bool allowServerEnemyInput)
        {
            if ((!IsOwner && !allowServerEnemyInput) ||
                _commandBuilder == null ||
                _player.InputPipeline == null ||
                _player.RuntimeData == null)
            {
                return default;
            }

            ProcessedInputData input = _player.InputPipeline.Current.currentFrameData.Processed;

            // 新命令读取本地视角输入源
            // 不读取会被历史重放覆盖的 Authority 朝向
            CharacterInputCommand command = _commandBuilder.Build(
                tick: TimeManager.LocalTick,
                input: in input,
                viewYaw: _player.RuntimeData.ViewYaw,
                viewPitch: _player.RuntimeData.ViewPitch,
                pitchLimits: _player.Config.Core.PitchLimits);

            BuildMeleeReplicateInput(
                in input,
                ref command,
                out int meleeTargetObjectId,
                out Vector3 meleeTargetPosition,
                out float meleeTargetCollisionRadius);

            if (command.HasButton(CharacterInputButtons.Jump))
            {
                _player.InputPipeline.ConsumeJumpPressed();
            }

            if (command.HasButton(CharacterInputButtons.Roll))
            {
                _player.InputPipeline.ConsumeRollPressed();
            }

            if (command.HasButton(CharacterInputButtons.Dodge))
            {
                _player.InputPipeline.ConsumeDodgePressed();
            }

            if (command.HasButton(CharacterInputButtons.Slide))
            {
                _player.InputPipeline.ConsumeSlidePressed();
            }

            return new ReplicateData(
                command.Move,
                command.ViewYaw,
                command.ViewPitch,
                command.Buttons,
                meleeTargetObjectId,
                meleeTargetPosition,
                meleeTargetCollisionRadius);
        }

        /// <summary>
        /// 为近战攻击写入按钮状态和预测使用的冻结目标快照
        /// 当前攻击来源不存在时保留远程武器原有的瞄准与开火输入
        /// </summary>
        private void BuildMeleeReplicateInput(
            in ProcessedInputData input,
            ref CharacterInputCommand command,
            out int targetObjectId,
            out Vector3 targetPosition,
            out float targetCollisionRadius)
        {
            targetObjectId = NetworkObject.UNSET_OBJECTID_VALUE;
            targetPosition = Vector3.zero;
            targetCollisionRadius = 0f;

            bool lightAttackPressed = input.FirePressed;
            bool heavyAttackPressed = input.AimPressed;
            float tickDeltaTime = (float)TimeManager.TickDelta;

            /*
             * 所有近战来源都必须至少提供轻击定义
             * 无法提供时说明当前角色没有激活近战攻击来源
             */
            if (!TryResolveMeleeAttackDefinition(
                    MeleeAttackType.Light,
                    tickDeltaTime,
                    out MeleeAttackDefinition attackDefinition))
            {
                return;
            }

            /*
             * 近战来源把右键解释为重击
             * 不能再让同一输入进入瞄准模拟
             */
            command.Buttons &= ~CharacterInputButtons.Aim;

            if (!lightAttackPressed && !heavyAttackPressed)
            {
                return;
            }

            MeleeAttackType attackType =
                lightAttackPressed
                    ? MeleeAttackType.Light
                    : MeleeAttackType.Heavy;

            /*
             * 轻击定义已经取得
             * 只有单独请求重击时才需要重新解析重击通道
             */
            if (attackType == MeleeAttackType.Heavy &&
                !TryResolveMeleeAttackDefinition(
                    attackType,
                    tickDeltaTime,
                    out attackDefinition))
            {
                // 当前来源不支持重击时消费无效边沿，避免切换装备后补出旧输入
                _player.InputPipeline.ConsumeAimPressed();
                return;
            }

            /*
             * 攻击尚未进入衔接窗口时保留InputPipeline中的按键缓存
             * 等冷却归零后再生成真正的网络攻击命令
             */
            if (_runner == null || _runner.State.MeleeAttackCooldownTicksRemaining > 0u)
            {
                return;
            }

            /*
             * 同Tick同时按下时轻击优先
             * 两个输入缓存都要消费，避免下一Tick补出一次重击
             */
            if (lightAttackPressed)
            {
                command.Buttons |= CharacterInputButtons.MeleeLightAttack;
                _player.InputPipeline.ConsumeFirePressed();

                if (heavyAttackPressed)
                {
                    _player.InputPipeline.ConsumeAimPressed();
                }
            }
            else
            {
                command.Buttons |= CharacterInputButtons.MeleeHeavyAttack;
                _player.InputPipeline.ConsumeAimPressed();
            }

            if (!attackDefinition.EnableAdsorption || _meleeTargeting == null)
            {
                return;
            }

            if (!_meleeTargeting.TryCreateAdsorptionIntent(
                    command.Tick,
                    attackDefinition.AdsorptionMaximumTravelDistance,
                    out MeleeIntent intent))
            {
                return;
            }

            Transform targetRoot = intent.Target.RootTransform;
            NetworkObject targetNetworkObject =
                targetRoot != null
                    ? targetRoot.GetComponentInParent<NetworkObject>()
                    : null;

            // 联机吸附目标必须具有有效的网络身份
            if (targetNetworkObject == null ||
                !targetNetworkObject.IsSpawned ||
                targetNetworkObject.ObjectId == NetworkObject.UNSET_OBJECTID_VALUE)
            {
                return;
            }

            targetObjectId = targetNetworkObject.ObjectId;
            targetPosition = intent.SampledTargetPosition;
            targetCollisionRadius = intent.TargetCollisionRadius;
        }

        #endregion

        #region Prediction(预测)

        /// <summary>
        /// 拥有者客户端预测、服务器权威模拟以及校正重放
        /// 都会执行这个方法。
        /// </summary>
        [Replicate]
        private void PerformReplicate(
            ReplicateData data,
            ReplicateState replicateState = ReplicateState.Invalid,
            Channel channel = Channel.Unreliable)
        {
            if (_runner == null || _player.RuntimeData == null)
            {
                return;
            }

            CharacterInputCommand command = data.ToCommand();

            command = SanitizeCommand(in command);

            /*
             * 能否冲刺由角色运行时属性决定
             * 不接受客户端在网络包中直接声明
             */
            bool canSprint = !_player.RuntimeData.IsStaminaDepleted && _player.RuntimeData.CurrentStamina > 0f;

            bool isHandsEmpty = _player.RuntimeData.CurrentItem == null;

            float tickDeltaTime = (float)TimeManager.TickDelta;
            bool isDead = _player.RuntimeData.IsDead || _player.RuntimeData.Arbitration.IsDead;

            if (isDead)
            {
                _runner.CancelMeleeAttack();
            }
            else
            {
                TryProcessMeleeAttack(in data, in command, replicateState, tickDeltaTime);
            }

            CharacterSimulationState state = _runner.Simulate(in command, canSprint, isHandsEmpty, tickDeltaTime);

            if (IsServerInitialized && replicateState.ContainsTicked() && !replicateState.ContainsReplayed())
            {
                _serverSimulationState = state;
                _hasServerSimulationState = true;

                /*
                 * 只有服务器正常推进的 Tick 可以产生近战命中
                 * 客户端预测和 FishNet 校正重放不能重复产生副作用
                 */
                _serverMeleeHitProcessor.Process(_player, _meleeAttackSource, in state, tickDeltaTime, ObjectId, this);
            }

            /*
             * 纯观察客户端仍需执行 FishNet 的远端模拟
             * 但不能用默认或推测的 Replicate 数据覆盖服务器表现快照
             * Owner 与服务器继续使用模拟结果驱动自己的表现黑板
             */
            if (!IsPureObserverClient)
            {
                WriteInputToRuntimeData(in command);
                WriteStateToRuntimeData(in state);
            }
        }

        /// <summary>
        /// 检查 Replicate 是否包含唯一且真实的近战攻击边沿
        /// 丢包补帧和同时提交两种攻击都不能成立攻击
        /// </summary>
        private bool TryProcessMeleeAttack(
            in ReplicateData data,
            in CharacterInputCommand command,
            ReplicateState replicateState,
            float tickDeltaTime)
        {
            if (!replicateState.ContainsCreated() ||
                !TryResolveMeleeAttackType(
                    in command,
                    out MeleeAttackType attackType))
            {
                return false;
            }

            if (IsServerInitialized)
            {
                return TryProcessServerMeleeAttack(
                    in data,
                    in command,
                    attackType,
                    tickDeltaTime);
            }

            // 纯观察客户端不处理本地攻击预测
            if (!Owner.IsLocalClient)
            {
                return false;
            }

            return TryProcessOwnerMeleeAttack(
                in data,
                in command,
                attackType,
                tickDeltaTime);
        }

        /// <summary>
        /// 检查命令是否只请求了一种近战攻击并解析其攻击类型
        /// </summary>
        private static bool TryResolveMeleeAttackType(
            in CharacterInputCommand command,
            out MeleeAttackType attackType)
        {
            bool requestsLight = command.HasButton(CharacterInputButtons.MeleeLightAttack);

            bool requestsHeavy = command.HasButton(CharacterInputButtons.MeleeHeavyAttack);

            // 两者都没有或两者同时存在都不是合法攻击请求
            if (requestsLight == requestsHeavy)
            {
                attackType = MeleeAttackType.None;
                return false;
            }

            attackType = requestsLight ? MeleeAttackType.Light : MeleeAttackType.Heavy;

            return true;
        }

        /// <summary>
        /// 检查当前角色的攻击来源能否提供与请求通道一致的有效攻击定义
        /// </summary>
        private bool TryResolveMeleeAttackDefinition(
            MeleeAttackType attackType,
            float tickDeltaTime,
            out MeleeAttackDefinition definition)
        {
            definition = default;

            if (_meleeAttackSource == null || !_meleeAttackSource.TryGetAttackDefinition(attackType, tickDeltaTime, out definition))
            {
                return false;
            }

            return definition.IsValid && definition.AttackType == attackType;
        }

        /// <summary>
        /// 检查Owner本地攻击来源与预测冷却并成立一次近战攻击
        /// 吸附失败不会撤销已经成立的挥击
        /// </summary>
        private bool TryProcessOwnerMeleeAttack(
            in ReplicateData data,
            in CharacterInputCommand command,
            MeleeAttackType attackType,
            float tickDeltaTime)
        {
            if (!TryResolveMeleeAttackDefinition(
                    attackType,
                    tickDeltaTime,
                    out MeleeAttackDefinition definition))
            {
                return false;
            }

            if (!_runner.TryBeginMeleeAttack(
                    command.Tick,
                    definition.AttackType,
                    definition.Combo,
                    definition.SourceRevision,
                    tickDeltaTime))
            {
                return false;
            }

            // 没有吸附目标或当前攻击未启用吸附时，挥击仍然有效
            if (!definition.EnableAdsorption ||
                data.MeleeTargetObjectId == NetworkObject.UNSET_OBJECTID_VALUE)
            {
                return true;
            }

            var request = new MeleeAdsorptionSimulationRequest(
                command.Tick,
                data.MeleeTargetPosition,
                data.MeleeTargetCollisionRadius,
                definition.AdsorptionMaximumTravelDistance);

            if (!request.IsValid)
            {
                return true;
            }

            MeleeAdsorptionMotionProfile motionProfile = definition.AdsorptionMotionProfile;
            _runner.TryBeginMeleeAdsorption(in request, in motionProfile);
            return true;
        }

        /// <summary>
        /// 使用服务器权威攻击来源成立近战攻击并重新校验吸附目标
        /// 目标校验失败只取消吸附，不会取消已经成立的挥击
        /// </summary>
        private bool TryProcessServerMeleeAttack(
            in ReplicateData data,
            in CharacterInputCommand command,
            MeleeAttackType attackType,
            float tickDeltaTime)
        {
            if (!TryResolveMeleeAttackDefinition(
                    attackType,
                    tickDeltaTime,
                    out MeleeAttackDefinition definition))
            {
                return false;
            }

            if (!_runner.TryBeginMeleeAttack(
                    command.Tick,
                    definition.AttackType,
                    definition.Combo,
                    definition.SourceRevision,
                    tickDeltaTime))
            {
                return false;
            }

            if (!definition.EnableAdsorption ||
                data.MeleeTargetObjectId == NetworkObject.UNSET_OBJECTID_VALUE ||
                _meleeTargeting == null)
            {
                return true;
            }

            /*
             * 重新执行服务器自己的候选排序、角度和遮挡检查
             * 不使用客户端提交的位置或碰撞半径
             */
            if (!_meleeTargeting.TryCreateAdsorptionIntent(
                    command.Tick,
                    definition.AdsorptionMaximumTravelDistance,
                    out MeleeIntent authoritativeIntent))
            {
                return true;
            }

            Transform targetRoot = authoritativeIntent.Target.RootTransform;
            NetworkObject targetNetworkObject =
                targetRoot != null
                    ? targetRoot.GetComponentInParent<NetworkObject>()
                    : null;

            if (targetNetworkObject == null ||
                !targetNetworkObject.IsSpawned ||
                targetNetworkObject.ObjectId != data.MeleeTargetObjectId)
            {
                return true;
            }

            if (!authoritativeIntent.TryCreateSimulationRequest(
                    out MeleeAdsorptionSimulationRequest request))
            {
                return true;
            }

            MeleeAdsorptionMotionProfile motionProfile = definition.AdsorptionMotionProfile;
            _runner.TryBeginMeleeAdsorption(in request, in motionProfile);
            return true;
        }

        #endregion

        #region Reconcile(状态调和)

        /// <summary>
        /// 当前 Tick 模拟完成后创建权威状态
        /// FishNet 服务器会把它发送给拥有者客户端
        /// </summary>
        public override void CreateReconcile()
        {
            if (!_networkSimulationActive || _runner == null)
            {
                return;
            }

            CharacterSimulationState state = _runner.State;

            NiumaReconcileData data = new NiumaReconcileData(in state);

            PerformReconcile(data);
        }

        /// <summary>
        /// 客户端收到服务器权威状态后恢复模拟器
        /// 随后 FishNet 会重新执行该 Tick 之后的输入
        /// </summary>
        [Reconcile]
        private void PerformReconcile(NiumaReconcileData data, Channel channel = Channel.Unreliable)
        {
            if (_runner == null)
            {
                return;
            }

            CharacterSimulationState state = data.ToSimulationState();

            _runner.ApplyState(in state);

            /*
             * Reconcile 始终恢复模拟器状态
             * 纯观察客户端的表现黑板只接受服务器表现快照
             */
            if (!IsPureObserverClient)
            {
                WriteStateToRuntimeData(in state);
            }
        }

        #endregion

        #region Observer Presentation(观察者表现同步)

        /// <summary>
        /// 当前实例是否只是一个远端角色观察副本
        /// </summary>
        private bool IsPureObserverClient => IsClientInitialized && !IsServerInitialized && !Owner.IsLocalClient;

        /// <summary>
        /// 判断 candidate Tick 是否比 current Tick 更新
        /// 使用 uint 环形序列比较，能够正确处理最大值回绕到 0
        /// </summary>
        private static bool IsTickNewer(uint candidate, uint current)
        {
            const uint halfRange = 0x80000000u;

            uint forwardDistance = unchecked(candidate - current);

            return forwardDistance != 0u && forwardDistance < halfRange;
        }

        /// <summary>
        /// 服务器按照配置的 Tick 间隔发送表现快照
        /// 这里只发送高层状态，不发送动画片段或动画时间
        /// </summary>
        private void TrySendPresentationState()
        {
            if (!IsServerInitialized || _runner == null)
            {
                return;
            }

            int intervalTicks = Mathf.Max(1, _presentationSendIntervalTicks);

            CharacterSimulationState simulationState = _runner.State;

            if (simulationState.Tick % (uint)intervalTicks != 0u)
            {
                return;
            }

            var presentationState = new CharacterPresentationState(in simulationState);

            /*
             * 表现快照可以被更新的快照替代，
             * 因此使用 Unreliable，不要求旧快照重传。
             */
            ObserversReceivePresentationState(presentationState, Channel.Unreliable);
        }

        /// <summary>
        /// 由服务器发送给该 NetworkObject 的观察者
        /// 本阶段只验证快照是否正确到达
        /// 下一步才会把状态应用到 PlayerRuntimeData
        /// </summary>
        [ObserversRpc(ExcludeOwner = true, ExcludeServer = true, BufferLast = true)]
        private void ObserversReceivePresentationState(CharacterPresentationState presentationState, Channel channel = Channel.Unreliable)
        {
            /*
             * 纯观察客户端：
             * 1. 已经初始化客户端身份；
             * 2. 不是服务器或 Host 的服务器实例；
             * 3. 本机不是该角色的 Owner。
             */
            if (!IsPureObserverClient || _player == null || _player.RuntimeData == null)
            {
                return;
            }

            /*
             * Unreliable 可能发生乱序。
             * 已接受过快照后，只允许更新的 Tick 继续进入。
             */
            if (_hasReceivedPresentationSnapshot && !IsTickNewer(presentationState.Tick, _lastReceivedPresentationTick))
            {
                return;
            }

            // 观察者首次看到角色时，不补播该角色加入视野前的落地
            if (!_hasReceivedPresentationSnapshot)
            {
                _landingPresentation.SetBaseline(presentationState.LandingSequence);
            }

            _lastReceivedPresentationTick = presentationState.Tick;
            _hasReceivedPresentationSnapshot = true;

            ApplyPresentationState(in presentationState);

            PrintPresentationSnapshotDiagnostic(in presentationState, channel);
        }

        /// <summary>
        /// 重置快照与落地表现历史，清除上一次网络生命周期留下的标记
        /// </summary>
        private void ResetPresentationSnapshotTracking()
        {
            _lastReceivedPresentationTick = 0u;
            _hasReceivedPresentationSnapshot = false;

            _lastPresentationDiagnosticSnapshot = default;
            _hasPresentationDiagnosticSnapshot = false;
            _nextPresentationDiagnosticTime = 0f;

            _landingPresentation.Reset();

            // 所有权切换时模拟器可能仍存在，以当前历史重新建立基线
            if (_runner != null)
            {
                _landingPresentation.SetBaseline(_runner.State.LandingSequence);
            }

            if (_player != null && _player.RuntimeData != null)
            {
                PlayerRuntimeData data = _player.RuntimeData;

                data.JustLanded = false;
                data.JustLeftGround = false;
                data.FallHeightLevel = 0;
            }
        }

        #endregion


        #region Simulation Ownership(模拟执行者)

        /// <summary>
        /// 检查角色模拟依赖和配置是否齐全，再创建网络 Tick 模拟器并接管位移
        /// </summary>
        private bool TryTakeSimulationOwnership()
        {
            if (_player == null ||
                _player.BaseStats == null ||
                _player.CharacterController == null ||
                _player.InputPipeline == null ||
                _player.RuntimeData == null)
            {
                Debug.LogError("[NiumaFishNet] NiumaCharacterController 尚未完成初始化，无法取得模拟控制权。", this);

                return false;
            }

            if (_player.Config == null || _player.Config.Core == null)
            {
                Debug.LogError("[NiumaFishNet] 网络角色没有配置 " + "PlayerSO 或 CoreSO。", this);

                return false;
            }

            float tickDeltaTime = (float)TimeManager.TickDelta;

            CharacterSimulationConfig config =
                CharacterSimulationConfigFactory.Create(
                    _player.Config,
                    _player.BaseStats,
                    tickDeltaTime);

            var body = new CharacterControllerSimulationBody(_player.CharacterController);

            var traversalProbe = new PhysicsCharacterTraversalProbe(_player.Config.Vaulting, _player.CharacterController);

            _runner = new CharacterSimulationRunner(body, traversalProbe, in config);

            // 在首次模拟前建立基线，避免漏掉第一次真实落地
            _landingPresentation.SetBaseline(_runner.State.LandingSequence);

            _commandBuilder = new CharacterInputCommandBuilder();


            // 开始模拟前清除残留的跳跃和翻越表现意图
            // 不清除 InputPipeline，避免丢失尚未进入 Tick 的按键输入
            _player.RuntimeData.WantsToJump = false;
            _player.RuntimeData.WantsDoubleJump = false;
            _player.RuntimeData.WantsToVault = false;
            _player.RuntimeData.WantsLowVault = false;
            _player.RuntimeData.WantsHighVault = false;

            _networkSimulationActive = true;
            return true;
        }

        /// <summary>
        /// 网络对象停止时释放 Tick 模拟器，不恢复旧帧级移动逻辑
        /// </summary>
        private void ReleaseSimulationOwnership()
        {
            if (!_networkSimulationActive)
            {
                return;
            }

            _runner = null;
            _commandBuilder = null;
            _networkSimulationActive = false;

            Debug.Log("[NiumaFishNet] 网络驱动已释放角色模拟控制权。", this);
        }

        #endregion

        #region Validation(校验)

        /// <summary>
        /// 客户端拥有对象不代表其提交的数据可信。
        /// 客户端预测与服务器模拟都使用同一份清洗结果。
        /// </summary>
        private CharacterInputCommand SanitizeCommand(in CharacterInputCommand source)
        {
            Vector2 move = source.Move;

            if (!IsFinite(move.x) || !IsFinite(move.y))
            {
                move = Vector2.zero;
            }
            else
            {
                move = Vector2.ClampMagnitude(move, 1f);
            }

            float viewYaw = IsFinite(source.ViewYaw) ? Mathf.Repeat(source.ViewYaw, 360f) : _runner.State.Yaw;

            Vector2 pitchLimits = _player.Config.Core.PitchLimits;

            float minimumPitch = Mathf.Min(pitchLimits.x, pitchLimits.y);

            float maximumPitch = Mathf.Max(pitchLimits.x, pitchLimits.y);

            float viewPitch = IsFinite(source.ViewPitch) ?
                Mathf.Clamp(source.ViewPitch, minimumPitch, maximumPitch) :
                Mathf.Clamp(_runner.State.ViewPitch, minimumPitch, maximumPitch);

            CharacterInputButtons allowedButtons =
                CharacterInputButtons.Walk | CharacterInputButtons.Sprint |
                CharacterInputButtons.Jump | CharacterInputButtons.Dodge |
                CharacterInputButtons.Roll | CharacterInputButtons.Slide |
                CharacterInputButtons.Aim | CharacterInputButtons.MeleeLightAttack |
                CharacterInputButtons.MeleeHeavyAttack;

            CharacterInputButtons buttons = source.Buttons & allowedButtons;

            CharacterInputButtons meleeAttackButtons = CharacterInputButtons.MeleeLightAttack | CharacterInputButtons.MeleeHeavyAttack;

            // 同时声明轻击和重击属于无效命令
            if ((buttons & meleeAttackButtons) == meleeAttackButtons)
            {
                buttons &= ~meleeAttackButtons;
            }

            if ((buttons & CharacterInputButtons.Sprint) != 0)
            {
                buttons &= ~CharacterInputButtons.Walk;
            }

            return new CharacterInputCommand(source.Tick, move, viewYaw, viewPitch, buttons);

        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        #endregion

        #region Runtime Data Bridge

        /// <summary>
        /// 为没有本地输入的服务器副本写入命令数据
        /// 本地 Owner 的输入由输入管线维护，不接受历史重放覆盖
        /// </summary>
        private void WriteInputToRuntimeData(in CharacterInputCommand command)
        {
            if (Owner.IsLocalClient)
            {
                return;
            }

            PlayerRuntimeData data = _player.RuntimeData;

            data.MoveInput = command.Move;

            // 服务器上的远端角色没有本地视角输入处理器
            data.AuthorityYaw = Mathf.Repeat(command.ViewYaw, 360f);
        }

        private void WriteStateToRuntimeData(in CharacterSimulationState state)
        {
            PlayerRuntimeData data = _player.RuntimeData;

            bool useLocalView = Owner.IsLocalClient;

            // 本地 Owner 的相机和 IK 跟随实时视角
            // 服务器远端副本继续使用网络命令与模拟结果
            CharacterAimPresentationBridge.Apply(
                data,
                state.IsAiming,
                useLocalView ? data.ViewYaw : data.AuthorityYaw,
                useLocalView ? data.ViewPitch : state.ViewPitch);

            CharacterVaultPresentationBridge.Apply(
                data,
                state.VaultType,
                state.VaultTick,
                state.VaultWallNormal,
                state.VaultLedgePoint);

            CharacterActionPresentationBridge.Apply(
                data,
                state.ActionType,
                state.ActionTick,
                state.ActionDirection);

            MeleeAttackPresentationBridge.Apply(
                data,
                state.MeleeAttackSequence,
                state.MeleeAttackStartTick,
                state.MeleeAttackCooldownTicksRemaining,
                state.MeleeAttackType,
                state.MeleeComboStepIndex,
                state.Tick);

            ApplyAirborneTransition(data, state.IsGrounded, state.VerticalVelocity);

            _landingPresentation.Apply(
                data,
                _player.Config.JumpAndLanding,
                state.IsGrounded,
                state.LandingSequence,
                state.LastLandingFallHeight);

            ApplyDoubleJumpTransition(data, state.HasPerformedDoubleJumpInAir);
            /*
             * 远端副本不会运行 LocomotionIntentProcessor，
             * 因此必须由网络桥记录上一个运动档位。
             * PlayerStopState 会根据它选择走路、慢跑或冲刺停止动画。
            */
            if (data.CurrentLocomotionState != state.LocomotionState)
            {
                data.LastLocomotionState = data.CurrentLocomotionState;
            }

            data.CurrentYaw = state.Yaw;
            data.VerticalVelocity = state.VerticalVelocity;
            data.CurrentLocomotionState = state.LocomotionState;
            data.CurrentSpeed = state.SmoothSpeed;
            data.IsGrounded = state.IsGrounded;
            data.HasPerformedDoubleJumpInAir = state.HasPerformedDoubleJumpInAir;

            data.SimulationMotionPhase = state.MotionPhase;
            data.SimulationMotionPhaseTick = state.MotionPhaseTick;
            data.SimulationStartDirection = state.StartDirection;
            data.SimulationStartLocomotionState = state.StartLocomotionState;

            /*
             * 远端不运行本地意图处理器，
             * 这里补上表现层可能使用的世界移动方向。
             */
            data.DesiredWorldMoveDir = state.MotionPhase == CharacterMotionPhase.Idle ? Vector3.zero : state.LastMoveDirection;

        }

        /// <summary>
        /// 根据接地变化生成离地与跳跃意图，并更新持续下落状态
        /// 落地事件由落地表现桥识别，不在这里根据接地边沿重复生成
        /// </summary>
        private void ApplyAirborneTransition(
            PlayerRuntimeData data,
            bool nextIsGrounded,
            float nextVerticalVelocity)
        {
            bool justLeftGround = data.IsGrounded && !nextIsGrounded;

            // 同一帧可能推进多个 Tick，保留尚未被状态机处理的离地标记
            if (justLeftGround)
            {
                data.JustLeftGround = true;

                if (nextVerticalVelocity > 0f)
                {
                    data.WantsToJump = true;
                }
            }

            data.WantsToFall = !nextIsGrounded && nextVerticalVelocity < _player.Config.Core.FallVerticalVelocityThreshold;
        }

        /// <summary>
        /// 根据模拟状态中的二段跳消费标记生成一次性表现意图。
        /// 只有 false -> true 的边沿才触发动画。
        /// </summary>
        private static void ApplyDoubleJumpTransition(PlayerRuntimeData data, bool nextHasPerformedDoubleJumpInAir)
        {
            bool justPerformedDoubleJump = !data.HasPerformedDoubleJumpInAir && nextHasPerformedDoubleJumpInAir;

            if (!justPerformedDoubleJump)
            {
                return;
            }

            data.DoubleJumpDirection = DoubleJumpDirection.Up;
            data.WantsDoubleJump = true;
        }

        /// <summary>
        /// 把服务器世界移动方向转换成角色局部二维输入。
        /// MovementParameterProcessor 会使用它更新动画混合参数。
        /// </summary>
        private static Vector2 ConvertWorldDirectionToMoveInput(Vector3 worldDirection, float characterYaw)
        {
            if (worldDirection.sqrMagnitude < 0.0001f)
            {
                return Vector2.zero;
            }

            Quaternion inverseYaw = Quaternion.Euler(0f, -characterYaw, 0f);

            Vector3 localDirection = inverseYaw * worldDirection;

            return Vector2.ClampMagnitude(new Vector2(localDirection.x, localDirection.z), 1f);
        }

        /// <summary>
        /// 把服务器表现快照写入纯观察客户端的角色黑板。
        /// 这里只更新表现数据，不修改预测模拟器状态和角色位置
        /// </summary>
        private void ApplyPresentationState(in CharacterPresentationState presentationState)
        {
            PlayerRuntimeData data = _player.RuntimeData;

            CharacterAimPresentationBridge.Apply(
                data,
                presentationState.IsAiming,
                presentationState.Yaw,
                presentationState.ViewPitch);

            CharacterVaultPresentationBridge.Apply(
                data,
                presentationState.VaultType,
                presentationState.VaultTick,
                presentationState.VaultWallNormal,
                presentationState.VaultLedgePoint);


            CharacterActionPresentationBridge.Apply(
                data,
                presentationState.ActionType,
                presentationState.ActionTick,
                presentationState.ActionDirection);

            MeleeAttackPresentationBridge.Apply(
                data,
                presentationState.MeleeAttackSequence,
                presentationState.MeleeAttackStartTick,
                presentationState.MeleeAttackCooldownTicksRemaining,
                presentationState.MeleeAttackType,
                presentationState.MeleeComboStepIndex,
                presentationState.Tick);

            /*
             * 记录上一个速度档位。
             * PlayerStopState 会使用它选择对应的停止动画。
             */
            if (data.CurrentLocomotionState != presentationState.LocomotionState)
            {
                data.LastLocomotionState = data.CurrentLocomotionState;
            }

            data.CurrentYaw = presentationState.Yaw;
            data.VerticalVelocity = presentationState.VerticalVelocity;
            data.CurrentLocomotionState = presentationState.LocomotionState;
            data.CurrentSpeed = presentationState.Speed;

            ApplyAirborneTransition(data, presentationState.IsGrounded, presentationState.VerticalVelocity);

            _landingPresentation.Apply(
                data,
                _player.Config.JumpAndLanding,
                presentationState.IsGrounded,
                presentationState.LandingSequence,
                presentationState.LastLandingFallHeight);

            ApplyDoubleJumpTransition(data, presentationState.HasPerformedDoubleJumpInAir);
            data.IsGrounded = presentationState.IsGrounded;
            data.HasPerformedDoubleJumpInAir = presentationState.HasPerformedDoubleJumpInAir;
            data.SimulationMotionPhase = presentationState.MotionPhase;
            data.SimulationMotionPhaseTick = presentationState.MotionPhaseTick;
            data.SimulationStartDirection = presentationState.StartDirection;
            data.SimulationStartLocomotionState = presentationState.StartLocomotionState;

            Vector3 worldDirection = presentationState.MoveDirection;

            worldDirection.y = 0f;

            if (worldDirection.sqrMagnitude > 0.0001f)
            {
                worldDirection.Normalize();
            }
            else
            {
                worldDirection = Vector3.zero;
            }

            /*
             * Stopping 阶段仍保留上一移动方向
             * 让停止动画知道角色原来朝哪里移动
             */
            data.DesiredWorldMoveDir = presentationState.MotionPhase == CharacterMotionPhase.Idle ? Vector3.zero : worldDirection;

            /*
             * Starting/Moving 才表示玩家仍有移动输入
             * Stopping 虽然还有惯性速度，但输入已经松开
             */
            bool hasMoveInput = presentationState.MotionPhase == CharacterMotionPhase.Starting ||
                                presentationState.MotionPhase == CharacterMotionPhase.Moving;

            data.MoveInput = hasMoveInput ? ConvertWorldDirectionToMoveInput(worldDirection, presentationState.Yaw) : Vector2.zero;

        }

        #endregion

        #region Diagnostics(诊断)

        private void PrintTickHeartbeat()
        {
            if (!_logTickHeartbeat)
            {
                return;
            }

            ushort tickRate = TimeManager.TickRate;

            if (tickRate == 0)
            {
                return;
            }

            if (TimeManager.LocalTick % tickRate != 0)
            {
                return;
            }

            Debug.Log(
                $"[NiumaFishNet] Tick心跳:" +
                $"Tick={TimeManager.LocalTick}, " +
                $"TickRate={tickRate}, " +
                $"IsOwner={IsOwner}, " +
                $"Server={IsServerInitialized}, " +
                $"Client={IsClientInitialized}",
                this
            );
        }

        private void PrintPresentationSnapshotDiagnostic(
            in CharacterPresentationState presentationState,
            Channel channel)
        {
            if (!_logPresentationSnapshots)
            {
                return;
            }

            bool stateChanged = !_hasPresentationDiagnosticSnapshot ||
                                presentationState.MotionPhase != _lastPresentationDiagnosticSnapshot.MotionPhase ||
                                presentationState.LocomotionState != _lastPresentationDiagnosticSnapshot.LocomotionState ||
                                presentationState.IsAiming != _lastPresentationDiagnosticSnapshot.IsAiming;

            bool heartbeatDue =
                Time.unscaledTime >=
                _nextPresentationDiagnosticTime;

            if (!stateChanged && !heartbeatDue)
            {
                return;
            }

            _lastPresentationDiagnosticSnapshot = presentationState;

            _hasPresentationDiagnosticSnapshot = true;

            _nextPresentationDiagnosticTime = Time.unscaledTime + 1f;

            Debug.Log(
                $"[NiumaFishNet表现快照] 已应用：" +
                $"ObjectId={ObjectId}, " +
                $"Tick={presentationState.Tick}, " +
                $"Locomotion={presentationState.LocomotionState}, " +
                $"Aiming={presentationState.IsAiming}, " +
                $"Pitch={presentationState.ViewPitch:F2}, " +
                $"Phase={presentationState.MotionPhase}, " +
                $"PhaseTick={presentationState.MotionPhaseTick}, " +
                $"Direction={presentationState.StartDirection}, " +
                $"Speed={presentationState.Speed:F3}, " +
                $"Grounded={presentationState.IsGrounded}, " +
                $"Channel={channel}",
                this);
        }

        #endregion


    }
}

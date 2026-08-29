using UnityEngine;
using FishNet.Utility.Template;
using NiumaTPC.Character;
using NiumaTPC.Character.Simulation;
using NiumaTPC.FishNet.Prediction;
using NiumaTPC.Character.RuntimeData;
using FishNet.Object.Prediction;
using FishNet.Transporting;
using FishNet.Connection;
using FishNet.Object;

namespace NiumaTPC.FishNet
{
    /// <summary>
    /// NiumaTPC 与 FishNet 预测系统之间的适配器。
    ///
    /// 本类负责：
    /// 1. 接收 FishNet 固定 Tick。
    /// 2. 管理角色移动控制权。
    /// 3. 后续提交 Replicate 与 Reconcile。
    ///
    /// 它不负责实现具体移动规则；
    /// 具体移动仍由 CharacterSimulationRunner 执行。
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
        [Tooltip("离线固定 Tick 驱动；网络启动时会自动禁用,防止离线驱动和 FishNet 同时移动角色。")]
        private OfflineCharacterSimulationDriver _offlineDriver;

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

        private CharacterSimulationRunner _runner;

        /// <summary>
        /// FishNet 当前是否已经取得角色模拟控制权。
        /// </summary>
        private bool _networkSimulationActive;

        /// <summary>
        /// 网络接管前，角色是否已经处于外部状态驱动模式。
        /// 释放网络控制时需要恢复。
        /// </summary>
        private bool _externalStateDrivenBeforeNetwork;
        private bool _networkAppliedExternalStateDrive;

        /// <summary>
        /// 网络层是否已经为该角色应用本地输入所有权规则。
        /// </summary>
        private bool _inputOwnershipApplied;

        private bool _inputWasBlockedBeforeNetwork;
        private bool _networkBlockedInput;

        //FishNet 驱动接管和归还跳跃
        /// <summary>
        /// 保存网络驱动接管前，IsExternalJumpSimulationActive 原本的值。
        /// </summary>
        private bool _externalJumpSimulationBeforeNetwork;
        /// <summary>
        /// 记录“网络驱动是否真的执行过接管”
        /// 防止初始化失败、重复退出或尚未进入网络时，错误地恢复角色状态
        /// </summary>
        private bool _networkAppliedExternalJumpSimulation;

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

        #region Unity Lifecycle(Unity的生命周期)

        private void Awake()
        {
            if(_player == null)
            {
                _player = GetComponent<NiumaCharacterController>();
            }

            if(_offlineDriver == null)
            {
                TryGetComponent(out _offlineDriver);
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
        /// 只有本地拥有者可以让 NiumaTPC 从设备采集输入。
        /// 服务器副本和观察者副本只消费 FishNet 提供的 Replicate 数据。
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

            _networkBlockedInput = !hasLocalInputAuthority;

            _networkAppliedExternalStateDrive = !hasLocalInputAuthority;

            if (_networkBlockedInput)
            {
                _player.SetInputBlocked(true, clearBufferedInput: true);

                /*
                 * 远端副本不运行本地输入翻译器，
                 * 其移动黑板由 FishNet 模拟结果写入。
                 */
                _player.SetExternalSimulationStateDriven(true,clearLocalIntent: false);
            }

            _inputOwnershipApplied = true;

            Debug.Log(
                $"[NiumaFishNet] 输入所有权：" +
                $"ObjectId={ObjectId}, " +
                $"LocalOwner={hasLocalInputAuthority}, " +
                $"InputBlocked={_player.IsInputBlocked},"+
                $"ExternalStateDriven=" +
                $"{_player.IsExternalSimulationStateDriven}",
                this);
        }

        private void ReleaseInputOwnership()
        {
            if (!_inputOwnershipApplied)
            {
                return;
            }

            if (_player != null && _networkBlockedInput)
            {
                if (_player != null && _networkAppliedExternalStateDrive)
                {
                    _player.SetExternalSimulationStateDriven(
                        _externalStateDrivenBeforeNetwork,
                        clearLocalIntent: false);
                }
                // 仅撤销网络层施加的状态，保留进入网络前已有的阻断。
                _player.SetInputBlocked(
                    _inputWasBlockedBeforeNetwork,
                    clearBufferedInput: false);
            }

            _inputOwnershipApplied = false;
            _inputWasBlockedBeforeNetwork = false;
            _networkBlockedInput = false;
            _externalStateDrivenBeforeNetwork = false;
            _networkAppliedExternalStateDrive = false;
        }

        #endregion

        #region FishNet Tick

        protected override void TimeManager_OnTick()
        {
            if (!_networkSimulationActive)
            {
                return;
            }

            PrintTickHeartbeat();

             /*
             *
             * 1. 拥有者构造 CharacterInputCommand。
             * 2. 调用 FishNet Replicate。
             * 3. 客户端与服务器执行同一个模拟器。
             */

            NiumaReplicateData data =  BuildReplicateData();

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
        /// 只有本地拥有者读取当前设备输入。
        /// </summary>
        private NiumaReplicateData BuildReplicateData()
        {
            if(!IsOwner || _commandBuilder == null || _player.InputPipeline == null || _player.RuntimeData == null)
            {
                return default;
            }

            ProcessedInputData input = _player.InputPipeline.Current.currentFrameData.Processed;

            /*
             * 这里的 Tick 只是临时值。
             * NiumaReplicateData 进入 FishNet 后，
             * FishNet 会为其设置真正的网络 Tick。
             */
            CharacterInputCommand command = _commandBuilder.Build(
                tick: TimeManager.LocalTick,
                input: in input,
                viewYaw: _player.RuntimeData.AuthorityYaw
            );

            if (command.HasButton(CharacterInputButtons.Jump))
            {
                // 命令已经保存了跳跃事实，可以清除本地帧输入缓冲。
                _player.InputPipeline.ConsumeJumpPressed();
            }

            return new NiumaReplicateData(command.Move, command.ViewYaw, command.Buttons);
        }

        #endregion

        #region Prediction(预测)

        /// <summary>
        /// 拥有者客户端预测、服务器权威模拟以及校正重放
        /// 都会执行这个方法。
        /// </summary>
        [Replicate]
        private void PerformReplicate(
            NiumaReplicateData data,
            ReplicateState replicateState = ReplicateState.Invalid,
            Channel channel = Channel.Unreliable)
        {
            if(_runner == null || _player.RuntimeData == null)
            {
                return;
            }

            CharacterInputCommand command = data.ToCommand();

            command = SanitizeCommand(in command);

            /*
             * 能否冲刺由角色运行时属性决定，
             * 不接受客户端在网络包中直接声明。
             */
            bool canSprint = !_player.RuntimeData.IsStaminaDepleted && _player.RuntimeData.CurrentStamina > 0f;
            
            bool isHandsEmpty = _player.RuntimeData.CurrentItem == null;

            float tickDeltaTime = (float)TimeManager.TickDelta;

            CharacterSimulationState state = _runner.Simulate(in command, canSprint,isHandsEmpty ,tickDeltaTime);

            /*
             * 纯观察客户端仍需执行 FishNet 的远端模拟，
             * 但不能用默认或推测的 Replicate 数据覆盖服务器表现快照。
             * Owner 与服务器继续使用模拟结果驱动自己的表现黑板。
             */
            if (!IsPureObserverClient)
            {
                WriteInputToRuntimeData(in command);
                WriteStateToRuntimeData(in state);
            }
        }

        #endregion

        #region Reconcile(状态调和)

        /// <summary>
        /// 当前 Tick 模拟完成后创建权威状态。
        /// FishNet 服务器会把它发送给拥有者客户端。
        /// </summary>
        public override void CreateReconcile()
        {
            if(!_networkSimulationActive || _runner == null)
            {
                return;
            }

            CharacterSimulationState state = _runner.State;

            NiumaReconcileData data = new NiumaReconcileData(in state);

            PerformReconcile(data);
        }

        /// <summary>
        /// 客户端收到服务器权威状态后恢复模拟器，
        /// 随后 FishNet 会重新执行该 Tick 之后的输入。
        /// </summary>
        [Reconcile]
        private void PerformReconcile(NiumaReconcileData data, Channel channel = Channel.Unreliable)
        {
            if(_runner == null)
            {
                return;
            }

            CharacterSimulationState state = data.ToSimulationState();

            _runner.ApplyState(in state);

            /*
             * Reconcile 始终恢复模拟器状态；
             * 纯观察客户端的表现黑板只接受服务器表现快照。
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
        /// 判断 candidate Tick 是否比 current Tick 更新。
        /// 使用 uint 环形序列比较，能够正确处理最大值回绕到 0
        /// </summary>
        private static bool IsTickNewer(uint candidate, uint current)
        {
            const uint halfRange = 0x80000000u;

            uint forwardDistance = unchecked(candidate - current);

            return forwardDistance != 0u && forwardDistance < halfRange;
        }

        /// <summary>
        /// 服务器按照配置的 Tick 间隔发送表现快照。
        /// 这里只发送高层状态，不发送动画片段或动画时间
        /// </summary>
        private void TrySendPresentationState()
        {
            if(!IsServerInitialized || _runner == null)
            {
                return;
            }

            int intervalTicks = Mathf.Max(1, _presentationSendIntervalTicks);

            CharacterSimulationState simulationState = _runner.State;

            if(simulationState.Tick % (uint)intervalTicks != 0u)
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
            if(!IsPureObserverClient || _player == null || _player.RuntimeData == null)
            {
                return;
            }

            /*
             * Unreliable 可能发生乱序。
             * 已接受过快照后，只允许更新的 Tick 继续进入。
             */
            if(_hasReceivedPresentationSnapshot && !IsTickNewer(presentationState.Tick, _lastReceivedPresentationTick))
            {
                return;
            }

            _lastReceivedPresentationTick = presentationState.Tick;
            _hasReceivedPresentationSnapshot = true;

            ApplyPresentationState(in presentationState);

            PrintPresentationSnapshotDiagnostic(in presentationState,channel);
        }

        /// <summary>
        /// 重置表现快照
        /// </summary>
        private void ResetPresentationSnapshotTracking()
        {
            _lastReceivedPresentationTick = 0u;
            _hasReceivedPresentationSnapshot = false;

            _lastPresentationDiagnosticSnapshot = default;
            _hasPresentationDiagnosticSnapshot = false;
            _nextPresentationDiagnosticTime = 0f;
        }

        #endregion


        #region Simulation Ownership(模拟执行者)

        /// <summary>
        /// 初始化网络模拟器，并让它成为角色位移的唯一执行者
        /// </summary>
        private bool TryTakeSimulationOwnership()
        {
            if(_player == null || _player.MotionDriver == null)
            {
                Debug.LogError("[NiumaFishNet] NiumaCharacterController 尚未完成初始化，" + "无法取得模拟控制权。", this);

                return false;
            }

            if(_player.Config == null || _player.Config.Core == null)
            {
                Debug.LogError("[NiumaFishNet] 网络角色没有配置 " + "PlayerSO 或 CoreSO。",this);

                return false;
            }

            float tickDeltaTime = (float)TimeManager.TickDelta;

            CharacterSimulationConfig config =
                CharacterSimulationConfigFactory.Create(
                    _player.Config,
                    tickDeltaTime);

            var body = new CharacterControllerSimulationBody(_player.CharacterController);

            _runner = new CharacterSimulationRunner(body, in config);

            _commandBuilder = new CharacterInputCommandBuilder();


            /*
             * 离线驱动只能用于无网络测试。
             * 网络启动后必须禁用，否则会出现一次 Tick 被移动两遍。
             */
            if(_offlineDriver != null && _offlineDriver.enabled)
            {
                _offlineDriver.enabled = false;

                Debug.Log("[NiumaFishNet] 已禁用离线固定 Tick 驱动。", this);
            }

            _player.MotionDriver.SetExternalSimulationActive(true);

            _externalJumpSimulationBeforeNetwork = _player.IsExternalJumpSimulationActive;

            _player.SetExternalJumpSimulationActive(true);
            _networkAppliedExternalJumpSimulation = true;

            _networkSimulationActive = true;
            return true;
        }

        /// <summary>
        /// 网络对象停止时归还角色移动控制权
        /// </summary>
        private void ReleaseSimulationOwnership()
        {
            if (!_networkSimulationActive)
            {
                return;
            }

            if(_player != null)
            {
                if(_networkAppliedExternalJumpSimulation)
                {
                    _player.SetExternalJumpSimulationActive(_externalJumpSimulationBeforeNetwork);
                }

                if(_player.MotionDriver != null)
                {
                    _player.MotionDriver.SetExternalSimulationActive(false);
                }
            }

            _externalJumpSimulationBeforeNetwork = false;
            _networkAppliedExternalJumpSimulation = false;

            _runner = null;
            _commandBuilder = null;
            _networkSimulationActive = false;

            Debug.Log( "[NiumaFishNet] 网络驱动已释放角色模拟控制权。",this);
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

            if(!IsFinite(move.x) || !IsFinite(move.y))
            {
                move = Vector2.zero;
            }
            else
            {
                move = Vector2.ClampMagnitude(move, 1f);
            }

            float viewYaw = IsFinite(source.ViewYaw) ? Mathf.Repeat(source.ViewYaw, 360f) : _runner.State.Yaw;

            CharacterInputButtons allowedButtons = 
                CharacterInputButtons.Walk | CharacterInputButtons.Sprint |
                CharacterInputButtons.Jump;

            CharacterInputButtons buttons = source.Buttons & allowedButtons;

            if((buttons & CharacterInputButtons.Sprint) != 0)
            {
                buttons &= ~CharacterInputButtons.Walk;
            }

            return new CharacterInputCommand(source.Tick, move, viewYaw, buttons);

        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        #endregion

        #region Runtime Data Bridge

        private void WriteInputToRuntimeData(in CharacterInputCommand command)
        {
            _player.RuntimeData.MoveInput = command.Move;
        }

        private void WriteStateToRuntimeData(in CharacterSimulationState state)
        {
            PlayerRuntimeData data = _player.RuntimeData;

            ApplyAirborneTransition(data,state.IsGrounded,state.VerticalVelocity);

            /*
             * 远端副本不会运行 LocomotionIntentProcessor，
             * 因此必须由网络桥记录上一个运动档位。
             * PlayerStopState 会根据它选择走路、慢跑或冲刺停止动画。
            */
            if(data.CurrentLocomotionState != state.LocomotionState)
            {
                data.LastLocomotionState = data.CurrentLocomotionState;
            }

            data.CurrentYaw = state.Yaw;
            data.VerticalVelocity = state.VerticalVelocity;
            data.CurrentLocomotionState = state.LocomotionState;
            data.CurrentSpeed = state.SmoothSpeed;
            data.IsGrounded = state.IsGrounded;

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
        /// 根据前后接地状态生成一次性跳跃、离地、落地和下落意图。
        /// 远端副本不会运行本地 GameplayParameterProcessor，
        /// 因此这些事实必须由服务器状态快照推导。
        /// </summary>
        /// <param name="data"></param>
        /// <param name="nextIsGrounded"></param>
        /// <param name="nextVerticalVelocity"></param>
        private void ApplyAirborneTransition(
            PlayerRuntimeData data,
            bool nextIsGrounded,
            float nextVerticalVelocity)
        {
            bool wasGrounded = data.IsGrounded;

            bool justLeftGround = wasGrounded && !nextIsGrounded;

            bool justLanded = !wasGrounded && nextIsGrounded;

            data.JustLeftGround = justLeftGround;
            data.JustLanded = justLanded;

            if(justLeftGround && nextVerticalVelocity > 0f)
            {
                data.WantsToJump = true;
            }

            if(nextIsGrounded)
            {
                data.WantsToFall = false;
            }
            else
            {
                data.WantsToFall = nextVerticalVelocity < _player.Config.Core.FallVerticalVelocityThreshold;
            }
        }

        /// <summary>
        /// 把服务器世界移动方向转换成角色局部二维输入。
        /// MovementParameterProcessor 会使用它更新动画混合参数。
        /// </summary>
        private static Vector2 ConvertWorldDirectionToMoveInput(Vector3 worldDirection, float characterYaw)
        {
            if(worldDirection.sqrMagnitude < 0.0001f)
            {
                return Vector2.zero;
            }

            Quaternion inverseYaw = Quaternion.Euler(0f, -characterYaw, 0f);

            Vector3 localDirection = inverseYaw * worldDirection;

            return Vector2.ClampMagnitude(new Vector2(localDirection.x, localDirection.z),1f);
        }

        /// <summary>
        /// 把服务器表现快照写入纯观察客户端的角色黑板。
        /// 这里只更新表现数据，不修改预测模拟器状态和角色位置
        /// </summary>
        private void ApplyPresentationState(in CharacterPresentationState presentationState)
        {
            PlayerRuntimeData data = _player.RuntimeData;

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
            data.IsGrounded = presentationState.IsGrounded;
            data.SimulationMotionPhase = presentationState.MotionPhase;
            data.SimulationMotionPhaseTick = presentationState.MotionPhaseTick;
            data.SimulationStartDirection = presentationState.StartDirection;
            data.SimulationStartLocomotionState = presentationState.StartLocomotionState;

            Vector3 worldDirection = presentationState.MoveDirection;

            worldDirection.y = 0f;

            if(worldDirection.sqrMagnitude > 0.0001f)
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
            data.DesiredWorldMoveDir = presentationState.MotionPhase == CharacterMotionPhase.Idle? Vector3.zero : worldDirection;

            /*
             * Starting/Moving 才表示玩家仍有移动输入
             * Stopping 虽然还有惯性速度，但输入已经松开
             */
            bool hasMoveInput = presentationState.MotionPhase == CharacterMotionPhase.Starting ||
                                presentationState.MotionPhase == CharacterMotionPhase.Moving;

            data.MoveInput = hasMoveInput ? ConvertWorldDirectionToMoveInput( worldDirection, presentationState.Yaw) : Vector2.zero;

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

            if(tickRate == 0)
            {
                return;
            }

            if(TimeManager.LocalTick % tickRate != 0)
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
                                presentationState.LocomotionState != _lastPresentationDiagnosticSnapshot.LocomotionState;

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

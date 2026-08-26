using UnityEngine;
using FishNet.Utility.Template;
using NiumaTPC.Character;
using NiumaTPC.Character.Simulation;
using NiumaTPC.FishNet.Prediction;
using NiumaTPC.Character.RuntimeData;
using FishNet.Object.Prediction;
using FishNet.Transporting;
using FishNet.Connection;

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
        [Tooltip("离线固定 Tick 驱动；网络启动时会自动禁用，" +"防止离线驱动和 FishNet 同时移动角色。")]
        private OfflineCharacterSimulationDriver _offlineDriver;

        [Header("诊断")]
        [Tooltip("启用后每秒打印一次 FishNet Tick 和网络身份。")]
        [SerializeField]
        private bool _logTickHeartbeat = true;

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
        }

        public override void OnOwnershipClient(NetworkConnection prevOwner)
        {
            if (!_networkSimulationActive)
            {
                return;
            }

            // 换角色、观战接管等场景可能在对象存活期间转移 Owner。
            // 重新计算输入门，避免旧拥有者继续采集本机输入。
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
             * 在这里调用 CreateReconcile，
             * 把服务器权威状态交给拥有者客户端。
             */
            CreateReconcile();
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

            float tickDeltaTime = (float)TimeManager.TickDelta;

            CharacterSimulationState state = _runner.Simulate(in command, canSprint, tickDeltaTime);

            WriteInputToRuntimeData(in command);
            WriteStateToRuntimeData(in state);
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

            WriteStateToRuntimeData(in state);
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

            if(_player != null && _player.MotionDriver != null)
            {
                _player.MotionDriver.SetExternalSimulationActive(false);
            }

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

            CharacterInputButtons allowedButtons = CharacterInputButtons.Walk | CharacterInputButtons.Sprint;

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

        #endregion


    }
}

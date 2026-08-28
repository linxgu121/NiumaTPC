using NiumaTPC.Character.RuntimeData;
using UnityEngine;

namespace NiumaTPC.Character.Simulation
{
    /// <summary>
    /// 在不启动网络的情况下，以固定 Tick 驱动角色模拟。
    /// 用于验证网络无关的基础移动模拟是否正确。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NiumaCharacterController))]
    [DefaultExecutionOrder(-200)]
    public sealed class OfflineCharacterSimulationDriver : MonoBehaviour
    {
        #region Inspector

        [Header("离线固定 Tick 模拟")]

        [SerializeField]
        [Tooltip("启用后由固定 Tick 模拟器接管角色位移；关闭时继续使用旧 MotionDriver。")]
        private bool enableTickSimulation;

        [SerializeField]
        [Range(10, 120)]
        [Tooltip("每秒执行的模拟 Tick 数。推荐先使用 30。")]
        private int tickRate = 30;

        [SerializeField]
        [Range(1, 16)]
        [Tooltip("单帧最多补算的 Tick 数，防止严重卡顿时陷入无限追赶。")]
        private int maxCatchUpTicksPerFrame = 4;

        #endregion

        #region Runtime State(运行时状态)

        private NiumaCharacterController _player;
        private CharacterInputCommandBuilder _commandBuilder;
        private CharacterSimulationRunner _runner;

        private double _accumulator;
        private uint _tick;
        private bool _isRunning;

        public bool IsRunning => _isRunning;

        public CharacterSimulationState CurrentState =>
            _runner != null
                ? _runner.State
                : default;

        #endregion

        #region Unity Lifecycle(Unity生命周期)

        private void Awake()
        {
            _player =
                GetComponent<NiumaCharacterController>();
        }

        private void Start()
        {
            if (enableTickSimulation)
            {
                StartSimulation();
            }
        }

        private void Update()
        {
            // 支持在 Play Mode 中切换开关，方便对比新旧移动。
            if (enableTickSimulation && !_isRunning)
            {
                StartSimulation();
            }
            else if (!enableTickSimulation && _isRunning)
            {
                StopSimulation();
                return;
            }

            if (!_isRunning)
            {
                return;
            }

            RunPendingTicks();
        }

        private void OnDisable()
        {
            StopSimulation();
        }

        private void OnValidate()
        {
            tickRate = Mathf.Clamp(tickRate, 10, 120);

            maxCatchUpTicksPerFrame = Mathf.Clamp(
                maxCatchUpTicksPerFrame,
                1,
                16);
        }

        #endregion

        #region Simulation Lifetime(模拟生命周期)

        private void StartSimulation()
        {
            if (_isRunning)
            {
                return;
            }

            if (_player == null ||
                _player.CharacterController == null ||
                _player.MotionDriver == null ||
                _player.InputPipeline == null ||
                _player.RuntimeData == null)
            {
                Debug.LogError(
                    "[离线模拟] NiumaCharacterController 尚未完成初始化。",
                    this);

                enableTickSimulation = false;
                return;
            }

            if (_player.Config == null ||
                _player.Config.Core == null)
            {
                Debug.LogError(
                    "[离线模拟] 玩家没有配置 PlayerSO 或 CoreSO。",
                    this);

                enableTickSimulation = false;
                return;
            }

            float tickDeltaTime = 1f / tickRate;

            CharacterSimulationConfig config =
                CharacterSimulationConfigFactory.Create(
                    _player.Config,
                    tickDeltaTime);

            var body =
                new CharacterControllerSimulationBody(
                    _player.CharacterController);

            _runner = new CharacterSimulationRunner(
                body,
                in config);

            _commandBuilder =
                new CharacterInputCommandBuilder();

            _accumulator = 0d;
            _tick = 0u;

            // 必须最后才切换所有权。
            // 如果前面初始化失败，旧 MotionDriver 仍可正常工作。
            _player.MotionDriver.SetExternalSimulationActive(true);

            _isRunning = true;

            Debug.Log(
                $"[离线模拟] 已启动，TickRate={tickRate}。",
                this);
        }

        private void StopSimulation()
        {
            if (!_isRunning)
            {
                return;
            }

            if (_player != null &&
                _player.MotionDriver != null)
            {
                _player.MotionDriver
                    .SetExternalSimulationActive(false);
            }

            _runner = null;
            _commandBuilder = null;

            _accumulator = 0d;
            _isRunning = false;

            Debug.Log(
                "[离线模拟] 已停止，移动权归还旧 MotionDriver。",
                this);
        }

        #endregion

        #region Tick Loop

        private void RunPendingTicks()
        {
            double tickDeltaTime = 1d / tickRate;

            // 网络模拟时间不依赖 Time.timeScale。
            _accumulator += Time.unscaledDeltaTime;

            int executedTicks = 0;

            while (_accumulator >= tickDeltaTime &&
                   executedTicks < maxCatchUpTicksPerFrame)
            {
                SimulateTick((float)tickDeltaTime);

                _accumulator -= tickDeltaTime;
                executedTicks++;
            }

            if (executedTicks >= maxCatchUpTicksPerFrame &&
                _accumulator >= tickDeltaTime)
            {
                // 丢弃来不及补算的完整 Tick，只保留不足一个 Tick 的余数。
                // 防止卡顿后进入“越追赶越卡顿”的死亡螺旋。
                _accumulator %= tickDeltaTime;
            }
        }

        private void SimulateTick(float tickDeltaTime)
        {
            ProcessedInputData input =
                _player.InputPipeline
                    .Current
                    .currentFrameData
                    .Processed;

            float viewYaw =
                _player.RuntimeData.AuthorityYaw;

            unchecked
            {
                _tick++;
            }

            CharacterInputCommand command =
                _commandBuilder.Build(
                    _tick,
                    in input,
                    viewYaw);

            if (command.HasButton(CharacterInputButtons.Jump))
            {
                _player.InputPipeline.ConsumeJumpPressed();
            }

            bool canSprint =
                !_player.RuntimeData.IsStaminaDepleted &&
                _player.RuntimeData.CurrentStamina > 0f;

            CharacterSimulationState state =
                _runner.Simulate(
                    in command,
                    canSprint,
                    tickDeltaTime);

            WriteStateToRuntimeData(
                in command,
                in state);
        }

        #endregion

        #region Runtime Data Bridge

        private void WriteStateToRuntimeData(
            in CharacterInputCommand command,
            in CharacterSimulationState state)
        {
            PlayerRuntimeData data =
                _player.RuntimeData;

            data.MoveInput = command.Move;
            data.CurrentYaw = state.Yaw;
            data.VerticalVelocity = state.VerticalVelocity;
            data.IsGrounded = state.IsGrounded;
            data.CurrentSpeed = state.SmoothSpeed;
            data.CurrentLocomotionState =
                state.LocomotionState;
            data.SimulationMotionPhase = state.MotionPhase;
            data.SimulationMotionPhaseTick = state.MotionPhaseTick;
            data.SimulationStartDirection = state.StartDirection;
            data.SimulationStartLocomotionState =
                state.StartLocomotionState;
        }

        #endregion
    }
}

using NiumaTPC.Character;
using NiumaTPC.Character.Combat;
using NiumaTPC.Core.Combat;
using NiumaTPC.Core.Faction;
using NiumaTPC.Enemy.Combat;
using NiumaTPC.Enemy.Config;
using NiumaTPC.Enemy.Core;
using NiumaTPC.Enemy.Navigation;
using NiumaTPC.Enemy.Perception;
using UnityEngine;

namespace NiumaTPC.Enemy.Brain
{
    /// <summary>
    /// 服务器敌人AI的决策入口
    /// 只生成控制意图，不直接移动角色或播放动画
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NiumaCharacterController))]
    [RequireComponent(typeof(TeamAffiliation))]
    public sealed class EnemyBrain : MonoBehaviour
    {
        #region Inspector(面板配置)

        [Header("敌人定义")]

        [SerializeField]
        [Tooltip("当前敌人使用的静态配置")]
        private EnemyDefinitionSO definition;

        [Header("角色依赖")]

        [SerializeField]
        [Tooltip("当前敌人的NiumaCharacterController，未绑定时自动获取")]
        private NiumaCharacterController character;

        [SerializeField]
        [Tooltip("当前敌人的MeleeTargetSensor，允许绑定在角色子物体上")]
        private MeleeTargetSensor targetSensor;

        [SerializeField]
        [Tooltip("当前敌人的TeamAffiliation，未绑定时自动获取")]
        private TeamAffiliation teamAffiliation;

        [SerializeField]
        [Tooltip("敌人视线检测起点，未绑定时后续使用CharacterController中心")]
        private Transform perceptionOrigin;

        [Header("视线检测")]

        [SerializeField]
        [Tooltip("视线查询使用的碰撞层，必须同时包含场景障碍和目标角色碰撞层")]
        private LayerMask lineOfSightMask = ~0;

        [SerializeField]
        [Min(0f)]
        [Tooltip("视线检测球半径，0表示使用射线")]
        private float lineOfSightRadius = 0.05f;

        #endregion

        #region Runtime State(运行时状态)

        private EnemyTargetSelector _targetSelector;
        private EnemyTargetMemory _targetMemory;
        private IEnemyPathPlanner _pathPlanner;

        private EnemyRuntimeData _runtimeData;
        private bool _initialized;
        private bool _serverAuthorityActive;

        #endregion

        #region Public API(公共接口)

        public EnemyDefinitionSO Definition => definition;

        public EnemyRuntimeData RuntimeData => _runtimeData;

        public NiumaCharacterController Character => character;

        public MeleeTargetSensor TargetSensor => targetSensor;

        public TeamAffiliation TeamAffiliation => teamAffiliation;

        public Transform PerceptionOrigin => perceptionOrigin;

        public bool IsServerAuthorityActive => _serverAuthorityActive;

        /// <summary>
        /// 开启或关闭服务器AI控制权
        /// 关闭时清除目标、调度时间和控制意图
        /// </summary>
        public void SetServerAuthorityActive(bool active)
        {
            if (!_initialized || _serverAuthorityActive == active)
            {
                return;
            }

            _serverAuthorityActive = active;
            _runtimeData.Reset();
            _pathPlanner?.Reset();
        }

        /// <summary>
        /// 由服务器网络桥接器在固定Tick调用
        /// 客户端和观察者不能调用该入口生成AI决策
        /// </summary>
        public void RunServerTick(uint serverTick, float tickDeltaTime)
        {
            if (!_initialized || !_serverAuthorityActive || !IsPositiveFinite(tickDeltaTime))
            {
                return;
            }

            if (character.RuntimeData == null)
            {
                return;
            }

            if (character.RuntimeData.IsDead || character.RuntimeData.Arbitration.IsDead)
            {
                _targetMemory.ForgetTarget(_runtimeData);
                _runtimeData.DecisionState = EnemyDecisionState.Dead;
                _runtimeData.ControlIntent.Clear();
                return;
            }

            if (!HasReachedScheduledTick(serverTick, _runtimeData.NextDecisionTick))
            {
                return;
            }

            uint decisionIntervalTicks = ConvertSecondsToTicks(definition.DecisionIntervalSeconds, tickDeltaTime);
            _runtimeData.NextDecisionTick = unchecked(serverTick + decisionIntervalTicks);

            EvaluateDecision(serverTick, tickDeltaTime);
        }

        #endregion

        #region Unity Lifecycle(Unity 生命周期)

        private void Reset()
        {
            character = GetComponent<NiumaCharacterController>();
            teamAffiliation = GetComponent<TeamAffiliation>();
            targetSensor = GetComponentInChildren<MeleeTargetSensor>(true);
        }

        private void Awake()
        {
            _initialized = TryInitialize();
        }

        private void OnDisable()
        {
            _serverAuthorityActive = false;
            _runtimeData?.Reset();
            _pathPlanner?.Reset();
        }

        #endregion

        #region Initialization(初始化)

        /// <summary>
        /// 解析敌人Brain运行所需的角色、阵营、感知器与配置依赖
        /// 缺少必要依赖时停止AI初始化
        /// </summary>
        private bool TryInitialize()
        {
            if (character == null)
            {
                character = GetComponent<NiumaCharacterController>();
            }

            if (teamAffiliation == null)
            {
                teamAffiliation = GetComponent<TeamAffiliation>();
            }

            if (targetSensor == null)
            {
                targetSensor = GetComponentInChildren<MeleeTargetSensor>(true);
            }

            if (definition == null || definition.AttackProfile == null || character == null || teamAffiliation == null || targetSensor == null)
            {
                Debug.LogError(
                    "[敌人AI] 初始化失败，请检查EnemyDefinitionSO、EnemyAttackProfileSO、NiumaCharacterController、TeamAffiliation和MeleeTargetSensor",
                    this);

                return false;
            }

            _runtimeData = new EnemyRuntimeData();
            _targetSelector = new EnemyTargetSelector();
            _targetMemory = new EnemyTargetMemory();
            _pathPlanner = CreatePathPlanner();

            if (_pathPlanner == null)
            {
                return false;
            }

            targetSensor.SetDetectionRadius(definition.DetectionRadius);
            _runtimeData.Reset();
            return true;
        }

        private IEnemyPathPlanner CreatePathPlanner()
        {
            switch (definition.NavigationMode)
            {
                case EnemyNavigationMode.Direct:
                    return new DirectEnemyPathPlanner();

                case EnemyNavigationMode.NavMesh:
                    return new NavMeshEnemyPathPlanner(
                        definition.NavMeshAreaMask,
                        definition.NavMeshSampleDistance,
                        definition.NavMeshCornerReachDistance);

                default:
                    Debug.LogError(
                        $"[敌人AI] 不支持的导航模式：{definition.NavigationMode}",
                        this);

                    return null;
            }
        }

        #endregion

        #region Decision(决策)

        /// <summary>
        /// 执行一次选敌、目标记忆与追击决策
        /// 可见目标优先于旧目标记忆
        /// </summary>
        private void EvaluateDecision(uint serverTick, float tickDeltaTime)
        {
            uint memoryDurationTicks = ConvertSecondsToOptionalTicks(definition.TargetMemorySeconds, tickDeltaTime);

            bool targetVisible = TrySelectVisibleTarget(out MeleeTargetCandidate candidate);
            IMeleeTarget target;
            Vector3 targetPosition;

            if (targetVisible)
            {
                target = candidate.Target;
                _targetMemory.RememberVisibleTarget(_runtimeData, target, serverTick);
                targetPosition = target.AdsorptionPosition;
            }
            else if (!_targetMemory.TryGetRememberedTarget(
                         _runtimeData,
                         serverTick,
                         memoryDurationTicks,
                         out target,
                         out targetPosition))
            {
                EnterIdle();
                return;
            }

            Vector3 characterPosition = GetCharacterCenterPosition();
            float horizontalDistance = CalculateHorizontalDistance(characterPosition, targetPosition);

            if (horizontalDistance > definition.LoseTargetDistance)
            {
                _targetMemory.ForgetTarget(_runtimeData);
                EnterIdle();
                return;
            }

            EnemyAttackProfileSO attackProfile = definition.AttackProfile;
            float attackStartDistance = GetAttackStartDistance(target, attackProfile);

            if (targetVisible && horizontalDistance <= attackStartDistance)
            {
                Vector3 targetDirection = targetPosition - characterPosition;

                if (IsWithinAttackAngle(targetDirection, character.RuntimeData.CurrentYaw, attackProfile.AttackMaximumAngleFromForward))
                {
                    EnterAttackState(characterPosition, targetPosition);
                }
                else
                {
                    /*
                     * 当前移动模拟不能在非瞄准Idle状态原地转向
                     * 未对准时继续使用追击输入完成转向，不能提前提交攻击
                     */
                    EnterChaseState(characterPosition, targetPosition, serverTick, tickDeltaTime);
                }

                return;
            }

            EnterChaseState(characterPosition, targetPosition, serverTick, tickDeltaTime);
        }

        /// <summary>
        /// 从感知候选中选择当前可见的敌对目标
        /// </summary>
        private bool TrySelectVisibleTarget(out MeleeTargetCandidate candidate)
        {
            return _targetSelector.TrySelectTarget(
                targetSensor,
                transform,
                teamAffiliation,
                GetPerceptionOriginPosition(),
                transform.forward,
                definition.MaximumTargetAngleFromForward,
                definition.DetectionRadius,
                gameObject.scene.GetPhysicsScene(),
                lineOfSightMask,
                lineOfSightRadius,
                QueryTriggerInteraction.Collide,
                out candidate);
        }

        private Vector3 GetPerceptionOriginPosition()
        {
            return perceptionOrigin != null ? perceptionOrigin.position : GetCharacterCenterPosition();
        }

        private Vector3 GetCharacterCenterPosition()
        {
            CharacterController controller = character.CharacterController;

            if (controller != null && controller.enabled)
            {
                return controller.bounds.center;
            }

            return character.transform.position;
        }

        /// <summary>
        /// 根据双方碰撞半径与攻击配置计算允许发动攻击的中心距离
        /// AttackStartSurfaceDistance表示扣除碰撞半径后的表面距离
        /// </summary>
        private float GetAttackStartDistance(IMeleeTarget target, EnemyAttackProfileSO attackProfile)
        {
            CharacterController controller = character.CharacterController;
            float characterRadius = 0f;

            if (controller != null && controller.enabled)
            {
                Bounds bounds = controller.bounds;
                characterRadius = Mathf.Max(bounds.extents.x, bounds.extents.z);
            }

            return characterRadius + Mathf.Max(0f, target.CollisionRadius) + attackProfile.AttackStartSurfaceDistance;
        }

        private static float CalculateHorizontalDistance(Vector3 from, Vector3 to)
        {
            Vector3 displacement = to - from;
            displacement.y = 0f;
            return displacement.magnitude;
        }

        /// <summary>
        /// 检查目标方向是否位于怪物当前朝向允许发动攻击的角度内
        /// 目标与怪物水平位置重合时不再要求朝向
        /// </summary>
        private static bool IsWithinAttackAngle(Vector3 targetDirection, float currentYaw, float maximumAngle)
        {
            targetDirection.y = 0f;
            if (targetDirection.sqrMagnitude <= 0.000001f) return true;

            float targetYaw = CalculateYaw(targetDirection);
            return Mathf.Abs(Mathf.DeltaAngle(currentYaw, targetYaw)) <= Mathf.Clamp(maximumAngle, 0f, 180f);
        }

        private static float CalculateYaw(Vector3 worldDirection)
        {
            return Mathf.Repeat(Mathf.Atan2(worldDirection.x, worldDirection.z) * Mathf.Rad2Deg, 360f);
        }

        private void EnterIdle()
        {
            _runtimeData.DecisionState = EnemyDecisionState.Idle;
            _runtimeData.ControlIntent.Clear();
            ResetPath();
        }

        private void EnterChaseState(Vector3 characterPosition, Vector3 targetPosition, uint serverTick, float tickDeltaTime)
        {
            _runtimeData.DecisionState = EnemyDecisionState.Chase;
            _runtimeData.ControlIntent.Clear();

            if (HasReachedScheduledTick(serverTick, _runtimeData.NextRepathTick))
            {
                uint repathIntervalTicks = ConvertSecondsToTicks(definition.RepathIntervalSeconds, tickDeltaTime);
                _runtimeData.NextRepathTick = unchecked(serverTick + repathIntervalTicks);

                if (!_pathPlanner.TryRebuildPath(characterPosition, targetPosition))
                {
                    return;
                }
            }

            if (!_pathPlanner.TryGetNextDirection(characterPosition, out Vector3 worldDirection))
            {
                return;
            }

            _runtimeData.ControlIntent.Move = Vector2.up;
            _runtimeData.ControlIntent.ViewYaw = CalculateYaw(worldDirection);
            _runtimeData.ControlIntent.ViewPitch = 0f;
            _runtimeData.ControlIntent.SprintHeld = definition.AllowSprintChase;
        }

        private void EnterAttackState(Vector3 characterPosition, Vector3 targetPosition)
        {
            _runtimeData.DecisionState = EnemyDecisionState.Attack;
            _runtimeData.ControlIntent.Clear();
            ResetPath();

            Vector3 targetDirection = targetPosition - characterPosition;
            targetDirection.y = 0f;

            if (targetDirection.sqrMagnitude > 0.000001f)
            {
                _runtimeData.ControlIntent.ViewYaw = CalculateYaw(targetDirection);
            }

            // EnemyInputSource会把该一次性意图转换为FirePressed轻击边沿
            _runtimeData.ControlIntent.LightAttackPressed = true;
        }

        private void ResetPath()
        {
            _pathPlanner?.Reset();
            _runtimeData.NextRepathTick = 0u;
            _runtimeData.CurrentPathCornerIndex = -1;
        }

        #endregion

        #region Tick Helpers(Tick辅助方法)

        /// <summary>
        /// 检查当前Tick是否已经到达计划执行时间
        /// 使用有符号差值兼容uint回绕
        /// </summary>
        private static bool HasReachedScheduledTick(uint currentTick, uint scheduledTick)
        {
            return unchecked((int)(currentTick - scheduledTick)) >= 0;
        }

        /// <summary>
        /// 将秒数转换为至少一个固定Tick
        /// </summary>
        private static uint ConvertSecondsToTicks(float seconds, float tickDeltaTime)
        {
            int tickCount = Mathf.Max(1, Mathf.CeilToInt(Mathf.Max(0f, seconds) / tickDeltaTime));
            return (uint)tickCount;
        }

        /// <summary>
        /// 将可为零的持续时间转换成固定Tick数量
        /// 零表示目标离开视线后不继续保留记忆
        /// </summary>
        private static uint ConvertSecondsToOptionalTicks(float seconds, float tickDeltaTime)
        {
            if (seconds <= 0f || float.IsNaN(seconds) || float.IsInfinity(seconds))
            {
                return 0u;
            }

            return (uint)Mathf.Max(1, Mathf.CeilToInt(seconds / tickDeltaTime));
        }

        /// <summary>
        /// 检查固定Tick间隔是否为大于零的有限值
        /// </summary>
        private static bool IsPositiveFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;
        }

        #endregion
    }
}
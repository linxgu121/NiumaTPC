using System;
using System.Collections.Generic;
using NiumaTPC.Core.Object;
using NiumaTPC.Item;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NiumaTPC.FishNet
{
    /// <summary>
    /// 网络弹道的客户端表现
    /// 只处理视觉、音效与静态遮挡，不结算命中、扣弹或伤害
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AmmunitionPresenter : MonoBehaviour
    {
        #region Inspector

        [SerializeField]
        [Tooltip("绑定场景中的 SimpleObjectPoolSystem，用于子弹和命中特效的生成与回收")]
        private SimpleObjectPoolSystem _visualPool;

        [SerializeField]
        [Tooltip("填写本局所有可用的弹药配置，不是当前玩家携带的武器列表；DefinitionId 必须唯一")]
        private AmmunitionDefinitionSO[] _ammunitionDefinitions;

        [SerializeField, Min(0)]
        [Tooltip("同时保留的命中特效上限；达到上限时回收最早的特效，0 表示不生成命中特效")]
        private int _maxActiveImpacts = 128;

        [SerializeField, Min(0.1f)]
        [Tooltip("预测弹等待服务器发射通知的最长秒数，超时只回收画面，不释放预占弹量")]
        private float _predictionTimeout = 2f;

        [SerializeField, Min(0f)]
        [Tooltip("预测弹确认后消除位置误差的时间，0 表示立即校正，不影响服务器命中结果")]
        private float _confirmationBlendTime = 0.08f;

        [Header("飞行画面遮挡")]
        [SerializeField]
        [Tooltip("仅选择服务器也会阻挡子弹的静态墙体、地形层；排除角色、武器、特效和移动物体；Nothing 表示关闭")]
        private LayerMask _visualBlockMask;

        #endregion

        #region 运行时状态

        private const int MaxActiveFlights = 1024;
        private const int RecentEndCapacity = 2048;

        private sealed class Flight
        {
            public AmmunitionView View;
            public ulong BindingId;

            public ShotRequestKey RequestKey;
            public string DefinitionId;

            public Vector3 Origin;
            public Vector3 InitialVelocity;
            public Vector3 Acceleration;
            public float InitialAge;
            public float Lifetime;
            public float CollisionRadius;
            public double ReceivedAt;

            public double PredictionDeadline;
            public Vector3 CorrectionOffset;
        }

        private struct Impact
        {
            public AmmunitionImpactView View;
            public double ExpiresAt;
        }

        private readonly Dictionary<string, AmmunitionDefinitionSO> _definitions = new(StringComparer.Ordinal);

        private readonly Dictionary<ulong, Flight> _flights = new();
        private readonly List<ulong> _removeIds = new();
        private readonly List<Impact> _impacts = new();

        // 只保留最近的结束编号，避免缓存无限增长
        private readonly HashSet<ulong> _endedIds = new();
        private readonly Queue<ulong> _endedOrder = new();

        private SimpleObjectPoolSystem _pool;
        private ulong _lastSpawnId;
        private bool _running;

        // 复用已有查询器，只查询画面遮挡，不推进逻辑子弹
        private readonly AmmunitionCollision _visualCollision = new();

        // 首次显示时补查已经飞过的轨迹
        private const float MaxVisualCurveError = 0.01f;
        private const int MaxInitialVisualSteps = 128;

        private const int MaxPredictions = 64;

        private readonly Dictionary<ShotRequestKey, Flight> _predictions = new();
        private readonly List<ShotRequestKey> _removePredictions = new();

        // 防止已拒绝、超时或结束的预测被迟到消息重新播放
        private readonly HashSet<ShotRequestKey> _closedPredictions = new();
        private readonly Queue<ShotRequestKey> _closedPredictionOrder = new();

        private ulong _nextBindingId;

        // 只登记正在运行的客户端表现层
        private static readonly Dictionary<PhysicsScene, AmmunitionPresenter> ClientPresenters = new();

        private PhysicsScene _clientPhysicsScene;
        private bool _clientRegistered;

        #endregion

        #region 生命周期

        public void BeginSession()
        {
            EndSession();

            if (!isActiveAndEnabled)
            {
                return;
            }

            _pool = _visualPool;

            if (_pool == null)
            {
                Debug.LogError("[网络弹道表现] 没有绑定对象池", this);
                return;
            }

            AmmunitionDefinitionSO[] definitions = _ammunitionDefinitions ?? Array.Empty<AmmunitionDefinitionSO>();

            foreach (AmmunitionDefinitionSO definition in definitions)
            {
                if (definition == null)
                {
                    continue;
                }

                string id = definition.DefinitionId;

                // 这里只检查网络资源身份，不建立完整配置验证系统
                if (string.IsNullOrWhiteSpace(id) ||
                    _definitions.ContainsKey(id))
                {
                    Debug.LogError(
                        $"[网络弹道表现] 弹药 DefinitionId 为空或重复：{id}",
                        definition);

                    EndSession();
                    return;
                }

                _definitions.Add(id, definition);
            }

            _running = true;

            if (!RegisterClientPresenter())
            {
                EndSession();
            }
        }

        public void EndSession()
        {
            _running = false;
            UnregisterClientPresenter();

            _removePredictions.Clear();
            _removePredictions.AddRange(_predictions.Keys);

            foreach (ShotRequestKey key in _removePredictions)
            {
                CancelPrediction(key);
            }

            _removePredictions.Clear();

            _removeIds.Clear();
            _removeIds.AddRange(_flights.Keys);

            foreach (ulong id in _removeIds)
            {
                ReleaseFlight(id);
            }

            _removeIds.Clear();

            for (int i = _impacts.Count - 1; i >= 0; i--)
            {
                ReleaseImpact(i);
            }

            _definitions.Clear();
            _endedIds.Clear();
            _endedOrder.Clear();
            _closedPredictions.Clear();
            _closedPredictionOrder.Clear();

            _lastSpawnId = 0UL;
            _nextBindingId = 0UL;
            _pool = null;
        }

        private void OnDisable()
        {
            EndSession();
        }

        private void OnDestroy()
        {
            EndSession();
        }

        #endregion



        #region 客户端世界登记

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetClientRegistry()
        {
            ClientPresenters.Clear();
        }

        public static bool TryGetForScene(
            Scene scene,
            out AmmunitionPresenter presenter)
        {
            presenter = null;

            if (!scene.IsValid() || !scene.isLoaded)
            {
                return false;
            }

            PhysicsScene physicsScene = scene.GetPhysicsScene();

            if (!physicsScene.IsValid() ||
                !ClientPresenters.TryGetValue(physicsScene, out var candidate) ||
                candidate == null ||
                !candidate._running ||
                !candidate.isActiveAndEnabled)
            {
                return false;
            }

            presenter = candidate;
            return true;
        }

        private bool RegisterClientPresenter()
        {
            PhysicsScene physicsScene = gameObject.scene.GetPhysicsScene();

            if (!physicsScene.IsValid())
            {
                Debug.LogError("[网络弹道表现] 无效的客户端物理世界", this);
                return false;
            }

            if (ClientPresenters.TryGetValue(physicsScene, out var existing) &&
                existing != null &&
                existing != this)
            {
                Debug.LogError(
                    "[网络弹道表现] 同一物理世界存在重复的客户端表现层",
                    this);
                return false;
            }

            ClientPresenters[physicsScene] = this;
            _clientPhysicsScene = physicsScene;
            _clientRegistered = true;
            return true;
        }

        private void UnregisterClientPresenter()
        {
            if (_clientRegistered &&
                ClientPresenters.TryGetValue(_clientPhysicsScene, out var current) &&
                current == this)
            {
                ClientPresenters.Remove(_clientPhysicsScene);
            }

            _clientRegistered = false;
            _clientPhysicsScene = default;
        }

        #endregion

        #region 本地预测弹

        internal bool TryPlayPrediction(
            ShotRequestKey requestKey,
            RangedWeaponSO weapon,
            Ray aim)
        {
            if (!_running ||
                !requestKey.IsValid ||
                weapon == null ||
                weapon.Ammunition == null ||
                _predictions.ContainsKey(requestKey) ||
                _closedPredictions.Contains(requestKey) ||
                _predictions.Count >= MaxPredictions)
            {
                return false;
            }

            string definitionId = weapon.Ammunition.DefinitionId;

            if (!TryGetDefinition(definitionId, out var definition))
            {
                return false;
            }

            if (!TryCreateFlight(
                    requestKey,
                    definitionId,
                    aim.origin,
                    aim.direction * weapon.ProjectileSpeed,
                    Physics.gravity * definition.GravityScale,
                    definition.MaxLifetime,
                    0f,
                    out Flight flight))
            {
                return false;
            }

            flight.PredictionDeadline =
                Time.unscaledTimeAsDouble +
                Mathf.Max(0.1f, _predictionTimeout);

            _predictions.Add(requestKey, flight);
            return true;
        }

        internal void CancelPrediction(ShotRequestKey requestKey)
        {
            if (!requestKey.IsValid)
            {
                return;
            }

            RememberClosedPrediction(requestKey);

            if (!_predictions.TryGetValue(requestKey, out Flight flight))
            {
                return;
            }

            // 先断开记录，再交回对象池
            _predictions.Remove(requestKey);
            ReturnFlightView(flight);
        }

        private void RememberClosedPrediction(ShotRequestKey requestKey)
        {
            if (!_closedPredictions.Add(requestKey))
            {
                return;
            }

            _closedPredictionOrder.Enqueue(requestKey);

            if (_closedPredictionOrder.Count > RecentEndCapacity)
            {
                _closedPredictions.Remove(
                    _closedPredictionOrder.Dequeue());
            }
        }

        private bool TryCreateFlight(
            ShotRequestKey requestKey,
            string definitionId,
            Vector3 origin,
            Vector3 initialVelocity,
            Vector3 acceleration,
            float lifetime,
            float initialAge,
            out Flight flight)
        {
            flight = null;

            if (_flights.Count + _predictions.Count >= MaxActiveFlights ||
                initialAge >= lifetime ||
                !TryGetDefinition(definitionId, out var definition) ||
                definition.VisualPrefab == null)
            {
                return false;
            }

            GameObject instance = SpawnVisual(definition.VisualPrefab);

            if (instance == null)
            {
                return false;
            }

            if (!instance.TryGetComponent(out AmmunitionView view))
            {
                Debug.LogError(
                    "[网络弹道表现] 子弹 Prefab 根节点缺少 AmmunitionView",
                    instance);

                ReturnVisual(instance);
                return false;
            }

            _nextBindingId = checked(_nextBindingId + 1UL);

            Flight created = new()
            {
                View = view,
                BindingId = _nextBindingId,
                RequestKey = requestKey,
                DefinitionId = definitionId,
                Origin = origin,
                InitialVelocity = initialVelocity,
                Acceleration = acceleration,
                Lifetime = lifetime,
                CollisionRadius = definition.CollisionRadius,
                InitialAge = initialAge,
                ReceivedAt = Time.unscaledTimeAsDouble
            };

            // 广播到达时子弹可能已经飞行了一段时间
            // 先检查这段路径，避免直接把视图生成在墙后
            if (!IsInitialVisualPathClear(created))
            {
                ReturnVisual(instance);
                return false;
            }

            CalculatePose(
                created,
                initialAge,
                out Vector3 position,
                out Vector3 velocity);

            if (!view.Bind(created.BindingId, position, velocity))
            {
                ReturnVisual(instance);
                return false;
            }

            flight = created;
            return true;
        }

        private static bool OwnsView(Flight flight)
        {
            return flight != null &&
                   flight.View != null &&
                   flight.View.BindingId == flight.BindingId;
        }

        private void ReturnFlightView(Flight flight)
        {
            // 不操作已经被对象池重新分配给其他飞行记录的视图
            if (OwnsView(flight))
            {
                ReturnVisual(flight.View.gameObject);
            }
        }

        #endregion


        #region 网络表现入口

        public void PlaySpawn(
            ulong shotId,
            string definitionId,
            ShotRequestKey requestKey,
            Vector3 origin,
            Vector3 initialVelocity,
            Vector3 acceleration,
            float lifetime,
            float initialAge)
        {
            if (!_running ||
                !requestKey.IsValid ||
                shotId == 0UL ||
                shotId <= _lastSpawnId ||
                _endedIds.Contains(shotId))
            {
                return;
            }

            _lastSpawnId = shotId;

            // 已拒绝、超时或结束的预测，不重新生成飞行画面
            // 后续合法命中通知仍会正常处理
            if (_closedPredictions.Contains(requestKey))
            {
                return;
            }

            _predictions.TryGetValue(requestKey, out Flight flight);
            _predictions.Remove(requestKey);
            RememberClosedPrediction(requestKey);

            initialAge = Mathf.Clamp(initialAge, 0f, lifetime);

            if (initialAge >= lifetime)
            {
                ReturnFlightView(flight);
                return;
            }

            bool canReuse =
                OwnsView(flight) &&
                flight.View.isActiveAndEnabled &&
                string.Equals(
                    flight.DefinitionId,
                    definitionId,
                    StringComparison.Ordinal);

            if (canReuse)
            {
                Vector3 oldDisplayPosition = flight.View.transform.position;

                flight.Origin = origin;
                flight.InitialVelocity = initialVelocity;
                flight.Acceleration = acceleration;
                flight.Lifetime = lifetime;
                flight.InitialAge = initialAge;
                flight.ReceivedAt = Time.unscaledTimeAsDouble;

                CalculatePose(
                    flight,
                    initialAge,
                    out Vector3 authoritativePosition,
                    out _);

                // 保留确认瞬间的位置，之后逐渐消除偏差
                // 不重新 Bind，保留已有粒子与拖尾
                flight.CorrectionOffset =
                    oldDisplayPosition - authoritativePosition;
            }
            else
            {
                ReturnFlightView(flight);

                if (!TryCreateFlight(
                        requestKey,
                        definitionId,
                        origin,
                        initialVelocity,
                        acceleration,
                        lifetime,
                        initialAge,
                        out flight))
                {
                    return;
                }
            }

            _flights.Add(shotId, flight);
        }

        public void PlayEnd(
            ulong shotId,
            string definitionId,
            ShotRequestKey requestKey,
            bool hasHit,
            Vector3 point,
            Vector3 normal)
        {
            if (!_running ||
                !requestKey.IsValid ||
                shotId == 0UL ||
                !_endedIds.Add(shotId))
            {
                return;
            }

            // 即使结束通知先到，也能按原始请求找到预测弹
            CancelPrediction(requestKey);

            _endedOrder.Enqueue(shotId);

            if (_endedOrder.Count > RecentEndCapacity)
            {
                _endedIds.Remove(_endedOrder.Dequeue());
            }

            // 即使没有收到发射消息，仍可根据这条消息播放命中
            ReleaseFlight(shotId);

            if (!hasHit ||
                !TryGetDefinition(definitionId, out var definition))
            {
                return;
            }

            // 一次性命中音效与特效 Prefab 自带声音分别处理
            if (definition.ImpactSound != null)
            {
                AudioSource.PlayClipAtPoint(definition.ImpactSound, point);
            }

            int limit = Mathf.Max(0, _maxActiveImpacts);

            if (limit == 0 || definition.ImpactPrefab == null)
            {
                return;
            }

            while (_impacts.Count >= limit)
            {
                ReleaseImpact(0);
            }

            GameObject instance = SpawnVisual(definition.ImpactPrefab);

            if (instance == null)
            {
                return;
            }

            if (!instance.TryGetComponent(out AmmunitionImpactView view))
            {
                Debug.LogError(
                    "[网络弹道表现] 命中特效根节点缺少 AmmunitionImpactView",
                    instance);

                ReturnVisual(instance);
                return;
            }

            if (!view.PlayAt(point, normal))
            {
                ReturnVisual(instance);
                return;
            }

            _impacts.Add(new Impact
            {
                View = view,
                ExpiresAt = Time.unscaledTimeAsDouble + view.RetainTime
            });
        }

        #endregion

        #region 表现推进

        private void LateUpdate()
        {
            if (!_running)
            {
                return;
            }

            double now = Time.unscaledTimeAsDouble;

            // 推进尚未确认的预测弹
            _removePredictions.Clear();

            foreach (var pair in _predictions)
            {
                Flight flight = pair.Value;

                if (now >= flight.PredictionDeadline ||
                    !TryAdvanceFlight(flight, now))
                {
                    _removePredictions.Add(pair.Key);
                }
            }

            foreach (ShotRequestKey key in _removePredictions)
            {
                CancelPrediction(key);
            }

            _removePredictions.Clear();

            // 推进已经确认的子弹
            _removeIds.Clear();

            foreach (var pair in _flights)
            {
                if (!TryAdvanceFlight(pair.Value, now))
                {
                    _removeIds.Add(pair.Key);
                }
            }

            foreach (ulong id in _removeIds)
            {
                ReleaseFlight(id);
            }

            _removeIds.Clear();

            for (int i = _impacts.Count - 1; i >= 0; i--)
            {
                Impact impact = _impacts[i];

                if (impact.View == null ||
                    !impact.View.isActiveAndEnabled ||
                    !impact.View.IsPlaying ||
                    now >= impact.ExpiresAt)
                {
                    ReleaseImpact(i);
                }
            }

            int limit = Mathf.Max(0, _maxActiveImpacts);

            while (_impacts.Count > limit)
            {
                ReleaseImpact(0);
            }
        }

        private bool TryAdvanceFlight(Flight flight, double now)
        {
            float age = flight.InitialAge + (float)(now - flight.ReceivedAt);

            if (!OwnsView(flight) ||
                !flight.View.isActiveAndEnabled ||
                age >= flight.Lifetime)
            {
                return false;
            }

            CalculatePose(
                flight,
                age,
                out Vector3 position,
                out Vector3 velocity);

            float correctionWeight = _confirmationBlendTime <= 0f
                ? 0f
                : 1f - Mathf.Clamp01(
                    (float)(now - flight.ReceivedAt) /
                    _confirmationBlendTime);

            position += flight.CorrectionOffset * correctionWeight;

            // 使用实际显示位置，确认阶段的平滑校正也不能穿过墙面
            if (!IsVisualSegmentClear(
                    flight,
                    flight.View.transform.position,
                    position))
            {
                return false;
            }

            return flight.View.ApplyPose(
                flight.BindingId,
                position,
                velocity);
        }

        private static void CalculatePose(
            Flight flight,
            float age,
            out Vector3 position,
            out Vector3 velocity)
        {
            // 与现有 AmmunitionMotion 使用相同的恒加速度公式
            // 这里只计算画面位置，不检测是否命中
            position =
                flight.Origin +
                flight.InitialVelocity * age +
                0.5f * flight.Acceleration * age * age;

            velocity =
                flight.InitialVelocity +
                flight.Acceleration * age;
        }

        #endregion

        #region 飞行画面遮挡

        /// <summary>
        /// 检查子弹首次显示前，从发射原点到 InitialAge 的整段轨迹
        /// 按重力曲线分段查询，避免迟到广播直接在墙后生成视图
        /// </summary>
        private bool IsInitialVisualPathClear(Flight flight)
        {
            if (_visualBlockMask.value == 0)
            {
                return true;
            }

            float duration = Mathf.Max(0f, flight.InitialAge);

            // 恒加速度曲线与弦的最大偏差为 |a|h²/8
            // 直线弹道只需要检查一段
            float requiredSteps = Mathf.Max(
                1f,
                Mathf.Sqrt(
                    flight.Acceleration.magnitude * duration * duration /
                    (8f * MaxVisualCurveError)));

            if (requiredSteps > MaxInitialVisualSteps)
            {
                // 只放弃飞行画面，服务器结束通知仍正常处理
                return false;
            }

            int stepCount = Mathf.CeilToInt(requiredSteps);
            Vector3 previousPosition = flight.Origin;

            for (int i = 1; i <= stepCount; i++)
            {
                float age = duration * (i / (float)stepCount);

                CalculatePose(
                    flight,
                    age,
                    out Vector3 nextPosition,
                    out _);

                if (!IsVisualSegmentClear(
                        flight,
                        previousPosition,
                        nextPosition))
                {
                    return false;
                }

                previousPosition = nextPosition;
            }

            return true;
        }

        /// <summary>
        /// 检查一段显示位移的起点重叠和沿途静态阻挡
        /// 仅检测 Visual Block Mask 指定的层，忽略 Trigger
        /// 不结算命中、播放命中特效或直接回收视图
        /// </summary>
        /// <param name="flight">提供本颗子弹的碰撞检测半径</param>
        /// <param name="from">线段起点，世界坐标</param>
        /// <param name="to">线段终点，世界坐标</param>
        private bool IsVisualSegmentClear(
            Flight flight,
            Vector3 from,
            Vector3 to)
        {
            // Nothing 表示不进行客户端画面遮挡查询
            if (_visualBlockMask.value == 0)
            {
                return true;
            }

            if (!_clientRegistered || !_clientPhysicsScene.IsValid())
            {
                return false;
            }

            // 临时承载查询参数，不加入 AmmunitionWorld
            // 这里的 0 不是一颗真实逻辑子弹的编号
            AmmunitionState probe = new(
                0UL,
                from,
                Vector3.zero,
                Vector3.zero,
                flight.CollisionRadius,
                1f);

            // 起点已经嵌入墙体时，也应停止显示
            if (_visualCollision.CheckInitialOverlap(
                    _clientPhysicsScene,
                    in probe,
                    null,
                    _visualBlockMask,
                    QueryTriggerInteraction.Ignore,
                    out _) != AmmunitionCastResult.Clear)
            {
                return false;
            }

            // 检查整段位移，而不是只检查这一帧的终点
            // 半径为 0 使用射线，否则使用球形扫掠
            return _visualCollision.Cast(
                _clientPhysicsScene,
                in probe,
                to,
                null,
                _visualBlockMask,
                QueryTriggerInteraction.Ignore,
                out _) == AmmunitionCastResult.Clear;
        }

        #endregion

        #region 资源与回收

        private bool TryGetDefinition(
            string definitionId,
            out AmmunitionDefinitionSO definition)
        {
            definition = null;

            if (!string.IsNullOrEmpty(definitionId) &&
                _definitions.TryGetValue(definitionId, out definition) &&
                definition != null)
            {
                return true;
            }

            Debug.LogError(
                $"[网络弹道表现] 没有对应的弹药资源：{definitionId}",
                this);

            return false;
        }

        private GameObject SpawnVisual(GameObject prefab)
        {
            if (_pool == null)
            {
                return null;
            }

            GameObject instance = _pool.Spawn(prefab);

            if (instance == null)
            {
                return null;
            }

            instance.transform.SetParent(null, true);

            if (instance.scene != gameObject.scene)
            {
                SceneManager.MoveGameObjectToScene(
                    instance,
                    gameObject.scene);
            }

            return instance;
        }

        private void ReleaseFlight(ulong shotId)
        {
            if (!_flights.TryGetValue(shotId, out Flight flight))
            {
                return;
            }

            _flights.Remove(shotId);
            ReturnFlightView(flight);
        }

        private void ReleaseImpact(int index)
        {
            Impact impact = _impacts[index];
            _impacts.RemoveAt(index);

            if (impact.View != null)
            {
                ReturnVisual(impact.View.gameObject);
            }
        }

        private void ReturnVisual(GameObject instance)
        {
            if (instance == null)
            {
                return;
            }

            // 场景退出时对象池可能已销毁，清理遗留的表现对象
            if (_pool == null || !_pool.TryDespawn(instance))
            {
                Destroy(instance);
            }
        }

        #endregion
    }
}
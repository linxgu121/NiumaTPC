using System;
using System.Collections.Generic;
using UnityEngine;
using NiumaResource;
using System.Threading.Tasks;

namespace NiumaTPC.Core.Object
{
    /// <summary>
    /// 基础对象池系统
    /// </summary>
    public sealed class ObjectPoolSystem : MonoBehaviour
    {
        [Serializable]
        public struct PrewarmEntry
        {
            public GameObject Prefab;
            [Min(0)] public int Count;
        }

        [Serializable]
        public struct AddressPrewarmEntry
        {
            [AddressableAddress(typeof(GameObject))]
            [Tooltip("需要提前加载和实例化的 Addressables 预制体地址")]
            public string Address;

            [Min(0)]
            [Tooltip("提前创建的实例数量，0 表示只加载 Prefab")]
            public int Count;
        }

        /// <summary>
        /// 一个地址池条目持有 Prefab Lease 以及由该 Prefab 创建的全部实例
        /// Lease 只能在闲置和活跃实例都清理后释放
        /// </summary>
        private sealed class AddressPoolEntry
        {
            public readonly string Address;
            public readonly Queue<GameObject> InactiveInstances;
            public readonly Dictionary<int, GameObject> ActiveInstances;

            public IResourceLease<GameObject> PrefabLease;
            public GameObject Prefab;
            public Task<AddressPoolPrepareResult> PrepareTask;
            public bool ReleaseRequested;

            public bool IsPrepared =>
                PrefabLease != null &&
                !PrefabLease.IsReleased &&
                Prefab != null;

            public AddressPoolEntry(string address, int initialCapacity)
            {
                Address = address;
                InactiveInstances = new Queue<GameObject>(initialCapacity);
                ActiveInstances = new Dictionary<int, GameObject>(initialCapacity);
            }
        }

        public static ObjectPoolSystem Shared { get; private set; }

        [Header("Direct Prefab Prewarm")]
        [SerializeField]
        [Tooltip("仅供不通过 Addressables 加载的场景固定资源使用")]
        private List<PrewarmEntry> _prewarm = new List<PrewarmEntry>();

        [Header("Addressables Prewarm")]
        [SerializeField]
        [Tooltip("地址池的预热配置，重复地址取最大的预热数量")]
        private List<AddressPrewarmEntry> _addressPrewarm = new List<AddressPrewarmEntry>();

        private readonly Dictionary<GameObject, Queue<GameObject>> _pool = new Dictionary<GameObject, Queue<GameObject>>(16);
        private readonly Dictionary<int, GameObject> _instanceToPrefab = new Dictionary<int, GameObject>(256);

        // 注: GetComponentsInChildren<T>() 每次调用都会返回新数组，必然产生 GC.Alloc
        // 对象池场景下 Spawn/Despawn 属于高频路径，因此对每个实例缓存其 IPoolable[]
        private readonly Dictionary<int, IPoolable[]> _instancePoolablesCache = new Dictionary<int, IPoolable[]>(256);

        // 地址使用区分大小写的稳定业务标识
        private readonly Dictionary<string, AddressPoolEntry> _addressPools = new Dictionary<string, AddressPoolEntry>(StringComparer.Ordinal);

        // 回收实例时通过 InstanceId 找到它所属的动态地址池
        private readonly Dictionary<int, AddressPoolEntry> _instanceToAddressPool = new Dictionary<int, AddressPoolEntry>(256);

        private bool _isDestroying;

        #region Unity Lifecycle(Unity 生命周期)
        private void Awake()
        {
            if (Shared != null && Shared != this)
            {
                Destroy(gameObject);
                return;
            }

            Shared = this;
            PrewarmAll();
        }

        private void OnDestroy()
        {
            // 重复对象被 Awake 销毁时不能清理真正的 Shared 实例
            if (Shared != this) return;

            _isDestroying = true;
            Shared = null;

            ForceReleaseAllAddressPools();
            ClearDirectPrefabPools();

            _instanceToAddressPool.Clear();
            _instancePoolablesCache.Clear();
        }

        #endregion


        private void PrewarmAll()
        {
            if (_prewarm == null) return;
            for (int i = 0; i < _prewarm.Count; i++)
            {
                var e = _prewarm[i];
                if (e.Prefab == null || e.Count <= 0) continue;
                Prewarm(e.Prefab, e.Count);
            }
        }

        /// <summary>
        /// 预热
        /// 优化:后期可以改为PrewarmAsync（异步预热）版本
        ///
        /// ----------架构的一些预热说明-----------
        /// 在高级框架里，有时候我们会把预热拆成两个阶段
        /// 加载预热：把 Addressable 的 Prefab 资源加载到内存
        /// 实例预热：造出真实的 GameObject
        /// 如果预热的数量极大（比如一波流的小怪），甚至可以不“实例化”它们
        /// 只“加载”底层 Prefab。等真的用到的时候，再快速 Instantiate
        /// （因为底层 IO 已经完成了，实例化很快）
        /// </summary>
        public void Prewarm(GameObject prefab, int count)
        {
            if (prefab == null || count <= 0) return;

            var q = GetOrCreateQueue(prefab);
            for (int i = 0; i < count; i++)
            {
                var inst = CreateInstance(prefab);
                InternalDespawn(inst, callCallbacks: true);
                q.Enqueue(inst);
            }
        }

        #region Address Pool Preparation(地址池准备)

        /// <summary>
        /// 根据对象池面板配置取得预热数量并准备地址池
        /// 未配置的地址只加载 Prefab，不提前创建实例
        /// </summary>
        public Task<AddressPoolPrepareResult> PrepareAddressPoolAsync(string address, IResourceService resourceService)
        {
            int prewarmCount = GetConfiguredAddressPrewarmCount(address);
            return PrepareAddressPoolAsync(address, resourceService, prewarmCount);
        }

        private int GetConfiguredAddressPrewarmCount(string address)
        {
            string normalizedAddress = address?.Trim() ?? string.Empty;

            if (string.IsNullOrEmpty(normalizedAddress) || _addressPrewarm == null)
                return 0;

            int configuredCount = 0;

            for (int i = 0; i < _addressPrewarm.Count; i++)
            {
                AddressPrewarmEntry entry = _addressPrewarm[i];
                string configuredAddress = entry.Address?.Trim() ?? string.Empty;

                if (string.Equals(configuredAddress, normalizedAddress, StringComparison.Ordinal))
                    configuredCount = Mathf.Max(configuredCount, Mathf.Max(0, entry.Count));
            }

            return configuredCount;
        }

        /// <summary>
        /// 检查地址、预热数量与资源服务后准备动态地址池
        /// 同地址并发调用共享同一个底层准备任务
        /// </summary>
        public async Task<AddressPoolPrepareResult> PrepareAddressPoolAsync(
            string address,
            IResourceService resourceService,
            int prewarmCount)
        {
            string normalizedAddress = address?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(normalizedAddress))
            {
                return AddressPoolPrepareResult.Failure(
                    normalizedAddress,
                    AddressPoolErrorCode.InvalidAddress,
                    "地址池资源地址为空");
            }

            if (prewarmCount < 0)
            {
                return AddressPoolPrepareResult.Failure(
                    normalizedAddress,
                    AddressPoolErrorCode.InvalidPrewarmCount,
                    "地址池预热数量不能小于零");
            }

            if (_isDestroying)
            {
                return AddressPoolPrepareResult.Failure(
                    normalizedAddress,
                    AddressPoolErrorCode.PoolSystemUnavailable,
                    "对象池正在关闭");
            }

            if (resourceService == null || !resourceService.IsInitialized)
            {
                return AddressPoolPrepareResult.Failure(
                    normalizedAddress,
                    AddressPoolErrorCode.ResourceServiceUnavailable,
                    "资源服务为空或尚未初始化");
            }

            if (_addressPools.TryGetValue(normalizedAddress, out AddressPoolEntry existingEntry))
            {
                if (existingEntry.ReleaseRequested)
                {
                    return AddressPoolPrepareResult.Failure(
                        normalizedAddress,
                        AddressPoolErrorCode.PoolReleasing,
                        "地址池正在等待释放");
                }

                if (existingEntry.IsPrepared)
                    return AddressPoolPrepareResult.Success(normalizedAddress);

                if (existingEntry.PrepareTask != null)
                    return await existingEntry.PrepareTask;

                // 未准备且没有加载任务的残留条目不能继续复用
                _addressPools.Remove(normalizedAddress);
            }

            var entry = new AddressPoolEntry(normalizedAddress, prewarmCount);
            _addressPools.Add(normalizedAddress, entry);

            Task<AddressPoolPrepareResult> prepareTask = PrepareAddressPoolCoreAsync(entry, resourceService, prewarmCount);

            entry.PrepareTask = prepareTask;
            AddressPoolPrepareResult result = await prepareTask;

            if (ReferenceEquals(entry.PrepareTask, prepareTask))
                entry.PrepareTask = null;

            return result;
        }

        private async Task<AddressPoolPrepareResult> PrepareAddressPoolCoreAsync(
            AddressPoolEntry entry,
            IResourceService resourceService,
            int prewarmCount)
        {
            IResourceLease<GameObject> loadedLease = null;

            try
            {
                ResourceLoadResult<GameObject> loadResult =
                    await resourceService.LoadAsync<GameObject>(entry.Address);

                if (!loadResult.Succeeded || loadResult.Lease == null)
                {
                    RemoveFailedAddressPoolEntry(entry);

                    return AddressPoolPrepareResult.Failure(
                        entry.Address,
                        AddressPoolErrorCode.ResourceLoadFailed,
                        loadResult.ErrorMessage,
                        loadResult.ErrorCode);
                }

                loadedLease = loadResult.Lease;

                if (_isDestroying)
                {
                    RemoveFailedAddressPoolEntry(entry);

                    return AddressPoolPrepareResult.Failure(
                        entry.Address,
                        AddressPoolErrorCode.PoolSystemUnavailable,
                        "资源加载完成时对象池已经开始关闭");
                }

                if (entry.ReleaseRequested)
                {
                    RemoveFailedAddressPoolEntry(entry);

                    return AddressPoolPrepareResult.Failure(
                        entry.Address,
                        AddressPoolErrorCode.PoolReleasing,
                        "资源加载期间地址池收到了释放请求");
                }

                GameObject prefab = loadedLease.Asset;

                if (prefab == null)
                {
                    RemoveFailedAddressPoolEntry(entry);

                    return AddressPoolPrepareResult.Failure(
                        entry.Address,
                        AddressPoolErrorCode.InvalidPrefab,
                        "资源 Lease 中没有可用的 GameObject Prefab");
                }

                entry.Prefab = prefab;

                for (int i = 0; i < prewarmCount; i++)
                {
                    GameObject instance = CreateAddressInstance(entry);
                    InternalDespawn(instance, callCallbacks: true);
                    entry.InactiveInstances.Enqueue(instance);
                }

                // 实例创建成功后才正式把 Lease 所有权交给池条目
                entry.PrefabLease = loadedLease;
                loadedLease = null;

                return AddressPoolPrepareResult.Success(entry.Address);
            }
            catch (Exception exception)
            {
                RemoveFailedAddressPoolEntry(entry);

                return AddressPoolPrepareResult.Failure(
                    entry.Address,
                    AddressPoolErrorCode.ResourceLoadFailed,
                    $"准备地址池时发生异常：{exception.Message}",
                    ResourceLoadErrorCode.LoadFailed);
            }
            finally
            {
                // 没有成功转交给池条目的 Lease 由本次准备流程释放
                loadedLease?.Dispose();
            }
        }

        private GameObject CreateAddressInstance(AddressPoolEntry entry)
        {
            GameObject instance = Instantiate(entry.Prefab);
            int instanceId = instance.GetInstanceID();

            _instanceToAddressPool[instanceId] = entry;
            CachePoolables(instance);

            return instance;
        }

        private void RemoveFailedAddressPoolEntry(AddressPoolEntry entry)
        {
            while (entry.InactiveInstances.Count > 0)
            {
                GameObject instance = entry.InactiveInstances.Dequeue();
                if (instance == null) continue;

                int instanceId = instance.GetInstanceID();
                _instanceToAddressPool.Remove(instanceId);
                _instancePoolablesCache.Remove(instanceId);
                Destroy(instance);
            }

            entry.Prefab = null;
            entry.PrefabLease?.Dispose();
            entry.PrefabLease = null;

            if (_addressPools.TryGetValue(entry.Address, out AddressPoolEntry currentEntry) &&
                ReferenceEquals(currentEntry, entry))
            {
                _addressPools.Remove(entry.Address);
            }
        }

        #endregion

        #region Address Pool Spawn(地址池取出)

        /// <summary>
        /// 检查地址池已经准备完成且未进入释放状态后取出实例
        /// 成功取出的实例会登记为活跃实例，必须归还当前对象池
        /// </summary>
        public bool TrySpawnByAddress(
            string address,
            out GameObject instance,
            out AddressPoolErrorCode errorCode)
        {
            instance = null;
            errorCode = AddressPoolErrorCode.None;

            string normalizedAddress = address?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(normalizedAddress))
            {
                errorCode = AddressPoolErrorCode.InvalidAddress;
                return false;
            }

            if (_isDestroying)
            {
                errorCode = AddressPoolErrorCode.PoolSystemUnavailable;
                return false;
            }

            if (!_addressPools.TryGetValue(normalizedAddress, out AddressPoolEntry entry) ||
                !entry.IsPrepared)
            {
                errorCode = AddressPoolErrorCode.PoolNotPrepared;
                return false;
            }

            if (entry.ReleaseRequested)
            {
                errorCode = AddressPoolErrorCode.PoolReleasing;
                return false;
            }

            GameObject candidate = null;

            try
            {
                // 跳过被外部错误销毁的闲置实例
                while (entry.InactiveInstances.Count > 0 && candidate == null)
                    candidate = entry.InactiveInstances.Dequeue();

                if (candidate == null)
                    candidate = CreateAddressInstance(entry);
            }
            catch (Exception exception)
            {
                errorCode = AddressPoolErrorCode.InstanceCreationFailed;

                Debug.LogError(
                    $"[ObjectPoolSystem] 创建地址池实例失败 Address={normalizedAddress}, " +
                    $"Exception={exception.Message}",
                    this);

                return false;
            }

            int instanceId = candidate.GetInstanceID();

            entry.ActiveInstances[instanceId] = candidate;
            _instanceToAddressPool[instanceId] = entry;

            InternalSpawn(candidate, callCallbacks: true);

            instance = candidate;
            return true;
        }

        #endregion

        #region Address Pool Release(地址池释放)

        /// <summary>
        /// 检查地址池存在后启动释放流程
        /// 请求成功只代表已经禁止继续取出，活跃实例可能仍在等待归还
        /// </summary>
        public bool RequestReleaseAddressPool(
            string address,
            out AddressPoolErrorCode errorCode)
        {
            errorCode = AddressPoolErrorCode.None;

            string normalizedAddress = address?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(normalizedAddress))
            {
                errorCode = AddressPoolErrorCode.InvalidAddress;
                return false;
            }

            if (_isDestroying)
            {
                errorCode = AddressPoolErrorCode.PoolSystemUnavailable;
                return false;
            }

            if (!_addressPools.TryGetValue(normalizedAddress, out AddressPoolEntry entry))
            {
                errorCode = AddressPoolErrorCode.PoolNotPrepared;
                return false;
            }

            // 重复请求保持安全，继续尝试完成尚未结束的释放
            entry.ReleaseRequested = true;
            ReleaseAddressPoolEntry(entry);

            return true;
        }

        private void ForceReleaseAllAddressPools()
        {
            if (_addressPools.Count == 0) return;

            // 先复制并清空索引，避免释放过程中修改正在遍历的 Dictionary
            var entries = new List<AddressPoolEntry>(_addressPools.Values);
            _addressPools.Clear();

            for (int i = 0; i < entries.Count; i++)
                ForceReleaseAddressPoolEntry(entries[i]);
        }

        private void ForceReleaseAddressPoolEntry(AddressPoolEntry entry)
        {
            if (entry == null) return;

            entry.ReleaseRequested = true;

            bool isPreparing =
                entry.PrepareTask != null &&
                !entry.PrepareTask.IsCompleted;

            int activeCount = entry.ActiveInstances.Count;

            if (isPreparing || activeCount > 0)
            {
                Debug.LogWarning(
                    $"[ObjectPoolSystem] 强制关闭地址池 Address={entry.Address}, " +
                    $"Preparing={isPreparing}, Active={activeCount}",
                    this);
            }

            foreach (KeyValuePair<int, GameObject> pair in entry.ActiveInstances)
            {
                GameObject instance = pair.Value;
                if (instance == null) continue;

                InternalDespawn(instance, callCallbacks: true);
                DestroyAddressInstance(instance);
            }

            entry.ActiveInstances.Clear();

            while (entry.InactiveInstances.Count > 0)
            {
                GameObject instance = entry.InactiveInstances.Dequeue();
                DestroyAddressInstance(instance);
            }

            entry.Prefab = null;
            entry.PrepareTask = null;

            entry.PrefabLease?.Dispose();
            entry.PrefabLease = null;
        }

        #endregion

        /// <summary>
        /// Spawn：取出或创建一个实例 并激活
        /// 注意：不设置 parent 不改 transform 调用者自行定位
        /// </summary>
        public GameObject Spawn(GameObject prefab)
        {
            if (prefab == null) return null;

            var q = GetOrCreateQueue(prefab);

            GameObject inst = null;

            //跳过已被外部销毁的对象（惰性清理）
            while (q.Count > 0 && inst == null)
                inst = q.Dequeue();

            if (inst == null)
                inst = CreateInstance(prefab);

            InternalSpawn(inst, callCallbacks: true);
            return inst;
        }

        #region Despawn(统一回收)

        /// <summary>
        /// 检查实例属于直接 Prefab 池或动态地址池后执行回收
        /// 已经归还的地址池实例不能重复入队
        /// </summary>
        public bool TryDespawn(GameObject instance)
        {
            if (instance == null) return true;

            int instanceId = instance.GetInstanceID();

            if (_instanceToAddressPool.TryGetValue(instanceId, out AddressPoolEntry addressEntry))
                return TryDespawnAddressInstance(instance, instanceId, addressEntry);

            if (!_instanceToPrefab.TryGetValue(instanceId, out GameObject prefab) || prefab == null)
                return false;

            Queue<GameObject> queue = GetOrCreateQueue(prefab);
            InternalDespawn(instance, callCallbacks: true);
            queue.Enqueue(instance);

            return true;
        }

        public void Despawn(GameObject instance)
        {
            if (instance == null) return;
            if (TryDespawn(instance)) return;

            Debug.LogWarning(
                $"[ObjectPoolSystem] 实例不属于当前对象池或已经被归还 Instance={instance.name}",
                instance);
        }

        /// <summary>
        /// 检查地址实例仍处于活跃状态后回收
        /// 释放中的池会销毁归还实例，不再放回闲置队列
        /// </summary>
        private bool TryDespawnAddressInstance(
            GameObject instance,
            int instanceId,
            AddressPoolEntry entry)
        {
            if (entry == null || !entry.ActiveInstances.Remove(instanceId))
                return false;

            InternalDespawn(instance, callCallbacks: true);

            if (!entry.ReleaseRequested && !_isDestroying)
            {
                entry.InactiveInstances.Enqueue(instance);
                return true;
            }

            DestroyAddressInstance(instance);

            if (entry.ReleaseRequested)
                ReleaseAddressPoolEntry(entry);

            return true;
        }

        private void DestroyAddressInstance(GameObject instance)
        {
            if (instance == null) return;

            int instanceId = instance.GetInstanceID();

            _instanceToAddressPool.Remove(instanceId);
            _instancePoolablesCache.Remove(instanceId);

            Destroy(instance);
        }

        /// <summary>
        /// 立即销毁闲置实例，活跃实例全部归还后再释放 Prefab Lease
        /// 重复调用保持安全
        /// </summary>
        private void ReleaseAddressPoolEntry(AddressPoolEntry entry)
        {
            if (entry == null) return;

            entry.ReleaseRequested = true;

            // 闲置实例已经没有继续保留的价值，可以立即清理
            while (entry.InactiveInstances.Count > 0)
            {
                GameObject instance = entry.InactiveInstances.Dequeue();
                DestroyAddressInstance(instance);
            }

            // 活跃实例仍可能正在播放子弹或特效，不能提前释放 Prefab Lease
            if (entry.ActiveInstances.Count > 0)
                return;

            entry.Prefab = null;
            entry.PrepareTask = null;

            entry.PrefabLease?.Dispose();
            entry.PrefabLease = null;

            if (_addressPools.TryGetValue(entry.Address, out AddressPoolEntry currentEntry) &&
                ReferenceEquals(currentEntry, entry))
            {
                _addressPools.Remove(entry.Address);
            }
        }

        #endregion

        private void ClearDirectPrefabPools()
        {
            foreach (Queue<GameObject> queue in _pool.Values)
            {
                while (queue.Count > 0)
                {
                    GameObject instance = queue.Dequeue();
                    if (instance == null) continue;

                    int instanceId = instance.GetInstanceID();
                    _instanceToPrefab.Remove(instanceId);
                    _instancePoolablesCache.Remove(instanceId);

                    Destroy(instance);
                }
            }

            _pool.Clear();

            // 剩余记录代表尚未回池的直接 Prefab 实例
            if (_instanceToPrefab.Count > 0)
            {
                Debug.LogWarning(
                    $"[ObjectPoolSystem] 销毁时仍有直接 Prefab 实例未归还 Count={_instanceToPrefab.Count}",
                    this);
            }

            _instanceToPrefab.Clear();
        }

        private Queue<GameObject> GetOrCreateQueue(GameObject prefab)
        {
            if (!_pool.TryGetValue(prefab, out var q) || q == null)
            {
                // 扩容风险：Queue 默认容量较小 会随着 Enqueue 扩容分配
                // 这里尝试根据预热配置给一个更合理的初始容量
                int initialCapacity = 0;
                if (_prewarm != null)
                {
                    for (int i = 0; i < _prewarm.Count; i++)
                    {
                        if (_prewarm[i].Prefab == prefab)
                        {
                            initialCapacity = Mathf.Max(0, _prewarm[i].Count);
                            break;
                        }
                    }
                }

                q = initialCapacity > 0 ? new Queue<GameObject>(initialCapacity) : new Queue<GameObject>();
                _pool[prefab] = q;
            }
            return q;
        }

        private GameObject CreateInstance(GameObject prefab)
        {
            var inst = Instantiate(prefab);
            int id = inst.GetInstanceID();
            _instanceToPrefab[id] = prefab;

            // 预缓存 IPoolable[] 避免 Spawn/Despawn 时 GetComponentsInChildren 分配数组
            CachePoolables(inst);

            return inst;
        }

        private void CachePoolables(GameObject instance)
        {
            if (instance == null) return;

            int id = instance.GetInstanceID();
            if (_instancePoolablesCache.ContainsKey(id)) return;

            // GC RISK NOTE: 这个 API 返回数组 会产生一次性分配
            // 但这发生在“创建实例时”（低频） 可接受；并换取后续 Spawn/Despawn 0 GC
            var poolables = instance.GetComponentsInChildren<IPoolable>(true);
            _instancePoolablesCache[id] = poolables;

        }

        private void InternalSpawn(GameObject instance, bool callCallbacks)
        {
            if (instance == null) return;

            // 时序修复：先激活，确保 OnEnable/Start 等生命周期已就绪，然后再通知 IPoolable。
            instance.SetActive(true);

            if (callCallbacks)
            {
                int id = instance.GetInstanceID();
                if (!_instancePoolablesCache.TryGetValue(id, out var poolables) || poolables == null)
                {
                    CachePoolables(instance);
                    _instancePoolablesCache.TryGetValue(id, out poolables);
                }

                if (poolables != null)
                {
                    for (int i = 0; i < poolables.Length; i++)
                    {
                        try { poolables[i]?.OnSpawned(); } catch { }
                    }
                }
            }
        }

        private void InternalDespawn(GameObject instance, bool callCallbacks)
        {
            if (instance == null) return;

            // 回收时：先让对象清理自身状态，再失活。
            if (callCallbacks)
            {
                int id = instance.GetInstanceID();
                if (!_instancePoolablesCache.TryGetValue(id, out var poolables) || poolables == null)
                {
                    CachePoolables(instance);
                    _instancePoolablesCache.TryGetValue(id, out poolables);
                }

                if (poolables != null)
                {
                    for (int i = 0; i < poolables.Length; i++)
                    {
                        try { poolables[i]?.OnDespawned(); } catch { }
                    }
                }
            }

            instance.SetActive(false);
        }
    }
}

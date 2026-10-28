using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace NiumaResource
{
    /// <summary>
    /// 基于 Addressables 的统一资源加载服务
    /// 负责共享底层加载、引用计数和资源生命周期
    /// 所有公开方法应从 Unity 主线程调用
    /// </summary>
    public sealed class AddressableResourceService : IResourceService
    {
        #region Runtime State

        // 相同地址与资源类型共享一个底层 Addressables Handle
        private readonly Dictionary<ResourceCacheKey, AddressableResourceEntry> _entries = new Dictionary<ResourceCacheKey, AddressableResourceEntry>();

        private bool _isInitialized;
        private bool _isShuttingDown;

        #endregion

        #region Properties

        /// <summary>
        /// 服务是否已经初始化且允许接收加载请求
        /// </summary>
        public bool IsInitialized => _isInitialized && !_isShuttingDown;

        #endregion

        #region Lifecycle

        /// <summary>
        /// 初始化资源服务并允许接收加载请求
        /// 已关闭的服务实例不能重新初始化
        /// </summary>
        public void Initialize()
        {
            if (_isShuttingDown)
            {
                throw new InvalidOperationException("已经关闭的资源服务不能重新初始化，请创建新的服务实例");
            }

            if (_isInitialized)
            {
                return;
            }

            _isInitialized = true;
        }

        /// <summary>
        /// 停止接收加载请求并释放全部底层资源
        /// 重复调用不会重复释放 Addressables Handle
        /// </summary>
        public void Shutdown()
        {
            if (_isShuttingDown)
            {
                return;
            }

            _isShuttingDown = true;
            _isInitialized = false;

            foreach (AddressableResourceEntry entry in _entries.Values)
            {
                if (entry.ReferenceCount > 0)
                {
                    Debug.LogWarning($"[NiumaResource] 服务关闭时仍有未归还的资源 Lease: " + $"Key={entry.Key}, References={entry.ReferenceCount}");
                }

                entry.ReleaseHandle();
            }

            _entries.Clear();
        }

        #endregion

        #region Public API

        /// <summary>
        /// 检查服务状态与资源地址并异步获取指定类型的资源 Lease
        /// 相同地址和类型的并发请求会共享同一个底层加载任务
        /// </summary>
        public async Task<ResourceLoadResult<T>> LoadAsync<T>(string address) where T : UnityEngine.Object
        {
            if (_isShuttingDown)
            {
                return ResourceLoadResult<T>.Failure(ResourceLoadErrorCode.ServiceShuttingDown,"资源服务正在关闭或已经关闭");
            }

            if (!_isInitialized)
            {
                return ResourceLoadResult<T>.Failure(ResourceLoadErrorCode.ServiceNotInitialized, "资源服务尚未初始化");
            }

            if (string.IsNullOrWhiteSpace(address))
            {
                return ResourceLoadResult<T>.Failure(ResourceLoadErrorCode.InvalidAddress,"资源地址不能为空或只包含空格");
            }

            ResourceCacheKey key = new ResourceCacheKey(address, typeof(T));

            AddressableResourceEntry entry = null;
            bool referenceReserved = false;

            try
            {
                entry = GetOrCreateEntry<T>(key);

                // 在等待加载前预留引用
                // 防止另一个并发请求先取得并释放 Lease 后销毁底层 Handle
                entry.AddReference();
                referenceReserved = true;

                await entry.LoadTask;

                if (_isShuttingDown)
                {
                    // Shutdown 已经统一清理条目，无需再次减引用
                    referenceReserved = false;

                    return ResourceLoadResult<T>.Failure(ResourceLoadErrorCode.ServiceShuttingDown,"资源加载期间服务已经关闭");
                }

                if (entry.IsReleased)
                {
                    referenceReserved = false;

                    return ResourceLoadResult<T>.Failure(ResourceLoadErrorCode.LoadFailed,$"资源加载条目已经失效: {key}");
                }

                if (entry.Status != AsyncOperationStatus.Succeeded)
                {
                    string operationError = entry.OperationException?.Message ?? "Addressables 未提供底层错误信息";

                    ReleaseEntryReference(entry);
                    referenceReserved = false;

                    return ResourceLoadResult<T>.Failure(ResourceLoadErrorCode.LoadFailed,$"资源加载失败: {key}, Error={operationError}");
                }

                // 检查 Addressables 返回对象是否符合调用方请求类型
                if (!entry.TryGetAsset<T>(out T asset))
                {
                    ReleaseEntryReference(entry);
                    referenceReserved = false;

                    return ResourceLoadResult<T>.Failure(ResourceLoadErrorCode.InvalidAssetType,$"资源类型与请求类型不一致: {key}");
                }

                var lease = new AddressableResourceLease<T>(entry,asset,ReleaseEntryReference);

                // 预留引用的所有权已经转移给 Lease
                referenceReserved = false;

                return ResourceLoadResult<T>.Success(lease);
            }
            catch (Exception exception)
            {
                if (referenceReserved)
                {
                    ReleaseEntryReference(entry);
                }

                ResourceLoadErrorCode errorCode = _isShuttingDown ? ResourceLoadErrorCode.ServiceShuttingDown : ResourceLoadErrorCode.LoadFailed;

                return ResourceLoadResult<T>.Failure(errorCode, $"资源加载发生异常: {key}, Error={exception.Message}");
            }
        }

        #endregion

        #region Cache Management

        /// <summary>
        /// 查找可复用的缓存条目，不存在时创建新的 Addressables 加载
        /// 已释放条目不会重新加入使用
        /// </summary>
        private AddressableResourceEntry GetOrCreateEntry<T>(ResourceCacheKey key) where T : UnityEngine.Object
        {
            if (_entries.TryGetValue(key,out AddressableResourceEntry existingEntry))
            {
                if (!existingEntry.IsReleased)
                {
                    return existingEntry;
                }

                _entries.Remove(key);
            }

            AsyncOperationHandle<T> handle = Addressables.LoadAssetAsync<T>(key.Address);

            var entry = new AddressableResourceEntry(key, handle);

            _entries.Add(key, entry);
            return entry;
        }

        /// <summary>
        /// 归还一次资源持有并在引用归零时移除缓存和释放底层 Handle
        /// 服务关闭后的延迟归还保持安全
        /// </summary>
        private void ReleaseEntryReference(AddressableResourceEntry entry)
        {
            if (entry == null || entry.IsReleased)
            {
                return;
            }

            int remainingReferences = entry.ReleaseReference();

            if (remainingReferences > 0)
            {
                return;
            }

            if (_entries.TryGetValue(entry.Key, out AddressableResourceEntry cachedEntry) && ReferenceEquals(cachedEntry, entry))
            {
                _entries.Remove(entry.Key);
            }

            entry.ReleaseHandle();
        }

        #endregion
    }
}
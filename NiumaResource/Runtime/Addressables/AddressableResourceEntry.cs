using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace NiumaResource
{
    /// <summary>
    /// 一次底层 Addressables 加载及其共享状态
    /// 相同地址和资源类型的请求共同使用一个条目
    /// </summary>
    internal sealed class AddressableResourceEntry
    {
        #region Runtime State
        // Addressables 操作的凭证
        private AsyncOperationHandle _handle;
        // 记录还有多少个业务模块正在使用这个资源
        private int _referenceCount;
        // 防止底层 Handle 被重复释放的幂等标记
        private bool _isReleased;

        #endregion

        #region Properties

        /// <summary>
        /// 当前条目对应的标准化资源身份
        /// </summary>
        public ResourceCacheKey Key { get; }

        /// <summary>
        /// 底层加载任务
        /// 相同资源的并发请求共同等待此任务
        /// </summary>
        public Task<object> LoadTask { get; }

        /// <summary>
        /// 当前登记的资源持有数量
        /// 包括等待加载完成且准备取得 Lease 的请求
        /// </summary>
        public int ReferenceCount => _referenceCount;

        /// <summary>
        /// 底层 Handle 是否已经释放
        /// </summary>
        public bool IsReleased => _isReleased;

        /// <summary>
        /// 底层加载是否已经结束
        /// </summary>
        public bool IsDone => !_isReleased && _handle.IsValid() && _handle.IsDone;

        /// <summary>
        /// 当前底层加载状态
        /// Handle 失效后返回 None
        /// </summary>
        public AsyncOperationStatus Status => _isReleased || !_handle.IsValid() ? AsyncOperationStatus.None : _handle.Status;

        /// <summary>
        /// 底层加载失败时产生的异常
        /// 仅用于日志诊断，不作为业务判断依据
        /// </summary>
        public Exception OperationException => _isReleased || !_handle.IsValid() ? null : _handle.OperationException;

        #endregion

        #region Constructor

        public AddressableResourceEntry(
            ResourceCacheKey key,
            AsyncOperationHandle handle)
        {
            if (!handle.IsValid())
            {
                throw new ArgumentException("资源缓存条目不能持有无效的 Addressables Handle",nameof(handle));
            }

            Key = key;
            _handle = handle;
            LoadTask = handle.Task;
        }

        #endregion

        #region Reference Counting

        /// <summary>
        /// 为一个加载请求登记资源持有
        /// 在等待底层加载前登记可防止并发请求之间提前释放资源
        /// </summary>
        public void AddReference()
        {
            if (_isReleased)
            {
                throw new InvalidOperationException($"已经释放的资源条目不能增加引用: {Key}");
            }

            checked
            {
                _referenceCount++;
            }
        }

        /// <summary>
        /// 释放一次资源持有并返回剩余引用数
        /// 服务关闭后的延迟释放保持幂等
        /// </summary>
        public int ReleaseReference()
        {
            if (_isReleased)
            {
                return 0;
            }

            if (_referenceCount <= 0)
            {
                throw new InvalidOperationException($"资源条目引用计数不能小于零: {Key}");
            }

            _referenceCount--;
            return _referenceCount;
        }

        #endregion

        #region Asset Access

        /// <summary>
        /// 检查加载状态与资源类型并取得强类型资源
        /// 已释放、加载失败或类型不匹配时返回失败
        /// </summary>
        public bool TryGetAsset<T>(out T asset) where T : UnityEngine.Object
        {
            asset = null;

            if (_isReleased ||
                !_handle.IsValid() ||
                _handle.Status != AsyncOperationStatus.Succeeded)
            {
                return false;
            }

            asset = _handle.Result as T;
            return asset != null;
        }

        #endregion

        #region Release

        /// <summary>
        /// 释放唯一底层 Handle 并使当前条目失效
        /// 重复调用不会重复释放 Addressables 资源
        /// </summary>
        public void ReleaseHandle()
        {
            if (_isReleased)
            {
                return;
            }

            _isReleased = true;
            _referenceCount = 0;

            if (_handle.IsValid())
            {
                Addressables.Release(_handle);
            }

            _handle = default;
        }

        #endregion
    }
}
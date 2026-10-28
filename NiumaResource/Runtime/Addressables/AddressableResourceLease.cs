using System;

namespace NiumaResource
{
    /// <summary>
    /// 调用方对一个已加载资源的独立持有关系
    /// 释放 Lease 只归还当前调用方占用的一个引用
    /// </summary>
    internal sealed class AddressableResourceLease<T> : IResourceLease<T> where T : UnityEngine.Object
    {
        #region Runtime State

        private AddressableResourceEntry _entry;
        // Lease 本身不负责减引用计数，它通过这个回调告诉外层管理器“我还回去了”
        private Action<AddressableResourceEntry> _releaseCallback;
        private T _asset;
        private bool _isDisposed;

        #endregion

        #region Properties

        /// <summary>
        /// 当前资源使用的稳定 Addressables 地址
        /// Lease 释放后仍可用于日志诊断
        /// </summary>
        public string Address { get; }

        /// <summary>
        /// 当前加载请求使用的资源类型
        /// Lease 释放后仍可用于日志诊断
        /// </summary>
        public Type AssetType { get; }

        /// <summary>
        /// 当前 Lease 是否已经主动释放或被资源服务强制失效
        /// </summary>
        public bool IsReleased => _isDisposed || _entry == null || _entry.IsReleased;

        /// <summary>
        /// 当前 Lease 持有的强类型资源
        /// Lease 失效后继续读取属于调用方生命周期错误
        /// </summary>
        public T Asset
        {
            get
            {
                if (IsReleased)
                {
                    throw new ObjectDisposedException(nameof(AddressableResourceLease<T>),$"资源 Lease 已经失效: {AssetType.FullName}:{Address}");
                }

                return _asset;
            }
        }

        #endregion

        #region Constructor

        public AddressableResourceLease(AddressableResourceEntry entry,T asset,Action<AddressableResourceEntry> releaseCallback)
        {
            if (entry == null)
            {
                throw new ArgumentNullException(nameof(entry));
            }

            if (entry.IsReleased)
            {
                throw new ArgumentException("不能为已经释放的资源条目创建 Lease",nameof(entry));
            }

            if (asset == null)
            {
                throw new ArgumentNullException(nameof(asset));
            }

            if (releaseCallback == null)
            {
                throw new ArgumentNullException(nameof(releaseCallback));
            }

            if (entry.Key.AssetType != typeof(T))
            {
                throw new ArgumentException(
                    $"Lease 类型与缓存条目类型不一致: " +
                    $"Lease={typeof(T).FullName}, Entry={entry.Key.AssetType.FullName}",
                    nameof(entry));
            }

            _entry = entry;
            _asset = asset;
            _releaseCallback = releaseCallback;

            Address = entry.Key.Address;
            AssetType = entry.Key.AssetType;
        }

        #endregion

        #region IDisposable

        /// <summary>
        /// 归还当前 Lease 对应的一次资源持有
        /// 重复调用不会重复减少引用计数
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;

            AddressableResourceEntry entry = _entry;
            Action<AddressableResourceEntry> releaseCallback = _releaseCallback;

            // 先清空自身引用，避免释放回调异常时再次 Dispose 重复归还
            _entry = null;
            _asset = null;
            _releaseCallback = null;

            releaseCallback?.Invoke(entry);
        }

        #endregion
    }
}
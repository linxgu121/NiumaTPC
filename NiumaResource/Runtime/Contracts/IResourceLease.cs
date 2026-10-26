using System;

namespace NiumaResource
{
    /// <summary>
    /// 一次资源持有关系
    /// 调用方使用结束后必须调用 Dispose 释放本次持有
    /// </summary>
    public interface IResourceLease : IDisposable
    {
        /// <summary>
        /// 当前资源在 Addressables 中使用的稳定地址
        /// </summary>
        string Address { get; }

        /// <summary>
        /// 本次加载请求使用的资源类型
        /// 用于诊断同一地址被错误类型请求的问题
        /// </summary>
        Type AssetType { get; }

        /// <summary>
        /// 当前 Lease 是否已经释放或被服务关闭强制失效
        /// </summary>
        bool IsReleased { get; }
    }

    /// <summary>
    /// 提供强类型资源访问的资源 Lease
    /// </summary>
    public interface IResourceLease<out T> : IResourceLease where T : UnityEngine.Object
    {
        /// <summary>
        /// 当前 Lease 持有的 Unity 资源
        /// Lease 释放后禁止继续访问
        /// </summary>
        T Asset { get; }
    }
}
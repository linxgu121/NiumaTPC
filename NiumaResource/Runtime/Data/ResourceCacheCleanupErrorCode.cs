namespace NiumaResource
{
    /// <summary>
    /// Bundle 缓存清理失败的稳定分类
    /// 业务层通过错误码决定记录日志或稍后重试
    /// </summary>
    public enum ResourceCacheCleanupErrorCode : byte
    {
        /// <summary>
        /// 清理成功，没有发生错误
        /// </summary>
        None = 0,

        /// <summary>
        /// 资源服务尚未完成初始化
        /// 当前 Catalog 还不能作为缓存保留依据
        /// </summary>
        ServiceNotInitialized = 1,

        /// <summary>
        /// 资源服务正在关闭或已经关闭
        /// 此时拒绝新的缓存维护请求
        /// </summary>
        ServiceShuttingDown = 2,

        /// <summary>
        /// Addressables 执行废弃 Bundle 清理失败
        /// 可能由缓存目录异常或底层操作失败导致
        /// </summary>
        CleanupFailed = 3
    }
}

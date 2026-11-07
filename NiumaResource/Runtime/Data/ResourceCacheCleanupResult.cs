using System;

namespace NiumaResource
{
    /// <summary>
    /// 一次 Bundle 缓存清理的最终结果
    /// 成功时错误码为 None，失败时提供稳定错误码和诊断信息
    /// </summary>
    public readonly struct ResourceCacheCleanupResult
    {
        #region Properties(结果属性)

        public bool Succeeded { get; }

        public ResourceCacheCleanupErrorCode ErrorCode { get; }

        public string ErrorMessage { get; }

        #endregion

        #region Constructor(构造方法)

        private ResourceCacheCleanupResult(bool succeeded, ResourceCacheCleanupErrorCode errorCode, string errorMessage)
        {
            Succeeded = succeeded;
            ErrorCode = errorCode;
            ErrorMessage = errorMessage ?? string.Empty;
        }

        #endregion

        #region Factory(结果创建)

        /// <summary>
        /// 创建 Bundle 缓存清理成功结果
        /// </summary>
        public static ResourceCacheCleanupResult Success()
        {
            return new ResourceCacheCleanupResult(true, ResourceCacheCleanupErrorCode.None, string.Empty);
        }

        /// <summary>
        /// 使用非 None 错误码创建 Bundle 缓存清理失败结果
        /// </summary>
        public static ResourceCacheCleanupResult Failure(ResourceCacheCleanupErrorCode errorCode, string errorMessage)
        {
            if (errorCode == ResourceCacheCleanupErrorCode.None)
            {
                throw new ArgumentOutOfRangeException(nameof(errorCode), "缓存清理失败结果不能使用 None 错误码");
            }

            return new ResourceCacheCleanupResult(false, errorCode, errorMessage);
        }

        #endregion
    }
}

using System;


namespace NiumaResource
{
    /// <summary>
    /// 一次异步资源加载的最终结果
    /// 成功时提供 Lease，失败时提供稳定错误码和诊断信息
    /// </summary>
    public readonly struct ResourceLoadResult<T> where T : UnityEngine.Object
    {
        #region Properties

        /// <summary>
        /// 本次加载是否成功获得资源 Lease
        /// </summary>
        public bool Succeeded { get; }

        /// <summary>
        /// 本次加载的错误分类
        /// 成功时固定为 None
        /// </summary>
        public ResourceLoadErrorCode ErrorCode { get; }

        /// <summary>
        /// 面向日志和开发者的错误详情
        /// 业务逻辑不能依赖该文本进行判断
        /// </summary>
        public string ErrorMessage { get; }

        /// <summary>
        /// 本次加载成功后获得的资源 Lease
        /// 失败时固定为 null
        /// </summary>
        public IResourceLease<T> Lease { get; }

        #endregion

        #region Constructor

        private ResourceLoadResult(
            bool succeeded,
            ResourceLoadErrorCode errorCode,
            string errorMessage,
            IResourceLease<T> lease)
        {
            Succeeded = succeeded;
            ErrorCode = errorCode;
            ErrorMessage = errorMessage;
            Lease = lease;
        }

        #endregion

        #region Factory

        /// <summary>
        /// 使用有效且尚未释放的 Lease 创建成功结果
        /// 无效 Lease 属于资源服务内部编程错误
        /// </summary>
        public static ResourceLoadResult<T> Success(IResourceLease<T> lease)
        {
            if (lease == null)
            {
                throw new ArgumentNullException(nameof(lease));
            }

            if (lease.IsReleased)
            {
                throw new ArgumentException("不能使用已经释放的 Lease 创建成功结果",nameof(lease));
            }

            return new ResourceLoadResult<T>(
                true,
                ResourceLoadErrorCode.None,
                string.Empty,
                lease);
        }

        /// <summary>
        /// 使用非 None 错误码创建失败结果
        /// 失败结果不会携带资源 Lease
        /// </summary>
        public static ResourceLoadResult<T> Failure(
            ResourceLoadErrorCode errorCode,
            string errorMessage)
        {
            if (errorCode == ResourceLoadErrorCode.None)
            {
                throw new ArgumentOutOfRangeException(nameof(errorCode),"失败结果不能使用 None 错误码");
            }

            return new ResourceLoadResult<T>(
                false,
                errorCode,
                errorMessage ?? string.Empty,
                null);
        }

        #endregion
    }
}
namespace NiumaCore.Network
{
    /// <summary>
    /// 统一网络错误码。
    /// 调用方根据枚举处理错误，避免依赖字符串匹配。
    /// </summary>
    public enum NetworkErrorCode
    {
        /// <summary>
        /// 没有错误，请求成功。
        /// </summary>
        None,

        /// <summary>
        /// 请求被调用方主动取消。
        /// </summary>
        Cancelled,

        /// <summary>
        /// 请求超过设定超时时间。
        /// </summary>
        Timeout,

        /// <summary>
        /// 当前设备无网络或无法连接服务器。
        /// </summary>
        NetworkUnavailable,

        /// <summary>
        /// 未登录、Token 失效或认证信息缺失。
        /// </summary>
        Unauthorized,

        /// <summary>
        /// 已认证但没有权限访问该资源。
        /// </summary>
        Forbidden,

        /// <summary>
        /// 请求的资源不存在。
        /// </summary>
        NotFound,

        /// <summary>
        /// 请求与服务器当前数据冲突，例如云存档版本冲突。
        /// </summary>
        Conflict,

        /// <summary>
        /// 服务器内部错误或后端服务不可用。
        /// </summary>
        ServerError,

        /// <summary>
        /// 请求或响应序列化失败。
        /// </summary>
        SerializationFailed,

        /// <summary>
        /// 未归类错误，需要通过日志进一步定位。
        /// </summary>
        Unknown
    }
}

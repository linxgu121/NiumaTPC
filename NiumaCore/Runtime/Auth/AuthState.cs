namespace NiumaCore.Auth
{
    /// <summary>
    /// 认证状态。
    /// </summary>
    public enum AuthState
    {
        /// <summary>
        /// 未登录状态。
        /// </summary>
        LoggedOut,

        /// <summary>
        /// 正在登录，请等待认证结果。
        /// </summary>
        LoggingIn,

        /// <summary>
        /// 已登录，并且当前 Token 可用。
        /// </summary>
        LoggedIn,

        /// <summary>
        /// Access Token 已过期，需要玩家重新登录
        /// </summary>
        Expired,

        /// <summary>
        /// 登录失败或认证服务返回错误。
        /// </summary>
        Failed
    }
}

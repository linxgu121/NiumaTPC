using System;

namespace NiumaCore.Auth
{
    /// <summary>
    /// Access Token 的运行时内存容器。
    /// 登录服务和 HTTP 客户端共享这个实例，避免复制两份 Token 状态。
    /// </summary>
    public sealed class RuntimeAccessTokenSession : IAccessTokenProvider
    {
        #region Public Properties

        public AuthToken CurrentToken { get; private set; }

        #endregion

        #region Public API

        public void SetToken(AuthToken token)
        {
            if (token == null)
            {
                throw new ArgumentNullException(nameof(token));
            }

            if (!token.IsValid)
            {
                throw new ArgumentException(
                    "不能保存已经过期的 Access Token。",
                    nameof(token));
            }

            CurrentToken = token;
        }

        public void Clear()
        {
            CurrentToken = null;
        }

        public bool TryGetAccessToken(
            out string accessToken)
        {
            AuthToken token = CurrentToken;

            if (token == null || !token.IsValid)
            {
                accessToken = string.Empty;
                return false;
            }

            accessToken = token.AccessToken;
            return true;
        }

        #endregion
    }
}
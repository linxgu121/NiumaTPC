using System;

namespace NiumaCore.Auth
{
    /// <summary>
    /// 当前登录会话持有的 Access Token。
    /// 只保存在运行时内存，不持久化账号密码。
    /// </summary>
    public sealed class AuthToken
    {
        #region Public Properties

        /// <summary>
        /// 后端返回的九位玩家公开 UID。
        /// 用于区分本地会话，不作为接口鉴权凭证。
        /// </summary>
        public string PlayerUid { get; }

        /// <summary>
        /// 调用受保护接口时使用的 JWT Access Token。
        /// </summary>
        public string AccessToken { get; }

        /// <summary>
        /// Token 的 UTC Unix 秒级过期时间。
        /// </summary>
        public long ExpiresAtUnixSeconds { get; }

        public DateTime ExpiresAtUtc =>
            DateTimeOffset
                .FromUnixTimeSeconds(ExpiresAtUnixSeconds)
                .UtcDateTime;

        public bool IsValid =>
            !string.IsNullOrWhiteSpace(AccessToken) &&
            DateTimeOffset.UtcNow.ToUnixTimeSeconds() <
            ExpiresAtUnixSeconds;

        #endregion

        public AuthToken(
            string playerUid,
            string accessToken,
            long expiresAtUnixSeconds)
        {
            if (string.IsNullOrWhiteSpace(playerUid))
            {
                throw new ArgumentException(
                    "玩家 UID 不能为空。",
                    nameof(playerUid));
            }

            if (string.IsNullOrWhiteSpace(accessToken))
            {
                throw new ArgumentException(
                    "Access Token 不能为空。",
                    nameof(accessToken));
            }

            if (expiresAtUnixSeconds <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(expiresAtUnixSeconds),
                    "Token 过期时间必须大于 0。");
            }

            PlayerUid = playerUid.Trim();
            AccessToken = accessToken.Trim();
            ExpiresAtUnixSeconds = expiresAtUnixSeconds;
        }
    }
}
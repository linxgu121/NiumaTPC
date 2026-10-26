using System;

namespace NiumaCore.Auth
{
    /// <summary>
    /// 登录意图。
    /// 具体 Java JSON DTO 由后端适配层创建。
    /// </summary>
    public sealed class LoginRequest
    {
        public string Username;

        /// <summary>
        /// 密码只用于本次请求，禁止通过 Unity 序列化持久化。
        /// </summary>
        [NonSerialized]
        public string Password;

        public LoginRequest(
            string username,
            string password)
        {
            Username = username;
            Password = password;
        }
    }
}
using System;

namespace NiumaCore.Auth
{
    /// <summary>
    /// 注册账号意图。
    /// 注册成功只创建账号和初始仓库，不自动登录。
    /// </summary>
    public sealed class RegisterRequest
    {
        public string Username;

        /// <summary>
        /// 密码只用于本次注册请求，不允许本地持久化。
        /// </summary>
        [NonSerialized]
        public string Password;

        public RegisterRequest(
            string username,
            string password)
        {
            Username = username;
            Password = password;
        }
    }
}
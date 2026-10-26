using System;

namespace NiumaCore.Auth
{
    /// <summary>
    /// 登录操作结果。
    /// Code 保存稳定的后端错误码，UI 不应根据中文 Message 判断业务。
    /// </summary>
    public readonly struct LoginResult
    {
        public readonly bool Succeeded;
        public readonly AuthToken Token;
        public readonly string Code;
        public readonly string Message;

        private LoginResult(
            bool succeeded,
            AuthToken token,
            string code,
            string message)
        {
            Succeeded = succeeded;
            Token = token;
            Code = code;
            Message = message;
        }

        public static LoginResult Success(
            AuthToken token)
        {
            if (token == null)
            {
                throw new ArgumentNullException(nameof(token));
            }

            return new LoginResult(
                true,
                token,
                "OK",
                string.Empty);
        }

        public static LoginResult Fail(
            string code,
            string message)
        {
            return new LoginResult(
                false,
                null,
                code ?? string.Empty,
                message ?? string.Empty);
        }
    }
}
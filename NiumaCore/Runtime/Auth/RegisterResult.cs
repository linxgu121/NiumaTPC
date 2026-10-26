using System;

namespace NiumaCore.Auth
{
    /// <summary>
    /// 注册操作结果。
    /// 注册成功不会携带 Token，玩家仍需执行登录。
    /// </summary>
    public readonly struct RegisterResult
    {
        public readonly bool Succeeded;
        public readonly Guid AccountUuid;
        public readonly string PlayerUid;
        public readonly Guid WarehouseUuid;
        public readonly string Code;
        public readonly string Message;

        private RegisterResult(
            bool succeeded,
            Guid accountUuid,
            string playerUid,
            Guid warehouseUuid,
            string code,
            string message)
        {
            Succeeded = succeeded;
            AccountUuid = accountUuid;
            PlayerUid = playerUid;
            WarehouseUuid = warehouseUuid;
            Code = code;
            Message = message;
        }

        public static RegisterResult Success(
            Guid accountUuid,
            string playerUid,
            Guid warehouseUuid)
        {
            return new RegisterResult(
                true,
                accountUuid,
                playerUid,
                warehouseUuid,
                "OK",
                string.Empty);
        }

        public static RegisterResult Fail(
            string code,
            string message)
        {
            return new RegisterResult(
                false,
                Guid.Empty,
                string.Empty,
                Guid.Empty,
                code ?? string.Empty,
                message ?? string.Empty);
        }
    }
}
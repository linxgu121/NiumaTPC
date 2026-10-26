using System;

namespace NiumaCore.Save
{
    /// <summary>
    /// 高层存档读取结果。
    /// 用于 ISaveService.LoadAsync，读取成功时返回明确的 SavePayload。
    /// </summary>
    [Serializable]
    public readonly struct SaveLoadResult
    {
        /// <summary>
        /// 是否成功读取到存档。
        /// </summary>
        public readonly bool Succeeded;

        /// <summary>
        /// 读取成功时返回的存档载荷。
        /// </summary>
        public readonly SavePayload Payload;

        /// <summary>
        /// 失败或调试提示信息。
        /// </summary>
        public readonly string Message;

        public SaveLoadResult(bool succeeded, SavePayload payload, string message)
        {
            Succeeded = succeeded;
            Payload = payload;
            Message = message;
        }

        public static SaveLoadResult Success(SavePayload payload)
        {
            return new SaveLoadResult(true, payload, null);
        }

        public static SaveLoadResult Fail(string message)
        {
            return new SaveLoadResult(false, null, message);
        }
    }
}

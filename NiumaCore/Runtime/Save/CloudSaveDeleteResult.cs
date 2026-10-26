using System;

namespace NiumaCore.Save
{
    /// <summary>
    /// 云端删除操作结果。
    /// 删除操作只表达成功或失败，不承载同步冲突信息。
    /// </summary>
    [Serializable]
    public readonly struct CloudSaveDeleteResult
    {
        /// <summary>
        /// 是否成功删除远端存档。
        /// </summary>
        public readonly bool Succeeded;

        /// <summary>
        /// 被删除的存档槽 ID。
        /// </summary>
        public readonly string SlotId;

        /// <summary>
        /// 失败或调试提示信息。
        /// </summary>
        public readonly string Message;

        public CloudSaveDeleteResult(bool succeeded, string slotId, string message)
        {
            Succeeded = succeeded;
            SlotId = slotId;
            Message = message;
        }

        public static CloudSaveDeleteResult Success(string slotId)
        {
            return new CloudSaveDeleteResult(true, slotId, null);
        }

        public static CloudSaveDeleteResult Fail(string slotId, string message)
        {
            return new CloudSaveDeleteResult(false, slotId, message);
        }
    }
}

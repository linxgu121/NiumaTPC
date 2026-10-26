using System;

namespace NiumaCore.Save
{
    /// <summary>
    /// 云端下载操作结果。
    /// 该结果只描述远端读取是否成功，不负责本地与远端冲突仲裁。
    /// </summary>
    [Serializable]
    public readonly struct CloudSaveDownloadResult
    {
        /// <summary>
        /// 是否成功从云端读取存档载荷。
        /// </summary>
        public readonly bool Succeeded;

        /// <summary>
        /// 下载成功时返回的远端存档载荷。
        /// </summary>
        public readonly SavePayload Payload;

        /// <summary>
        /// 失败或调试提示信息。
        /// </summary>
        public readonly string Message;

        public CloudSaveDownloadResult(bool succeeded, SavePayload payload, string message)
        {
            Succeeded = succeeded;
            Payload = payload;
            Message = message;
        }

        public static CloudSaveDownloadResult Success(SavePayload payload)
        {
            return new CloudSaveDownloadResult(true, payload, null);
        }

        public static CloudSaveDownloadResult Fail(string message)
        {
            return new CloudSaveDownloadResult(false, null, message);
        }
    }
}

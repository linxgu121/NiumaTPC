using System;

namespace NiumaCore.Save
{
    /// <summary>
    /// 云端元数据查询结果。
    /// 只描述服务器上是否存在该存档以及远端元数据，不表达本地同步状态。
    /// </summary>
    [Serializable]
    public readonly struct CloudSaveMetadataResult
    {
        /// <summary>
        /// 查询请求是否成功完成。
        /// </summary>
        public readonly bool Succeeded;

        /// <summary>
        /// 云端是否存在该存档槽。
        /// </summary>
        public readonly bool Exists;

        /// <summary>
        /// 云端存档元数据，不存在或请求失败时为空。
        /// </summary>
        public readonly SaveSlotMetadata Metadata;

        /// <summary>
        /// 失败或调试提示信息。
        /// </summary>
        public readonly string Message;

        public CloudSaveMetadataResult(bool succeeded, bool exists, SaveSlotMetadata metadata, string message)
        {
            Succeeded = succeeded;
            Exists = exists;
            Metadata = metadata;
            Message = message;
        }

        public static CloudSaveMetadataResult Found(SaveSlotMetadata metadata)
        {
            return new CloudSaveMetadataResult(true, true, metadata, null);
        }

        public static CloudSaveMetadataResult NotFound(string slotId)
        {
            return new CloudSaveMetadataResult(true, false, null, $"云端不存在存档槽：{slotId}");
        }

        public static CloudSaveMetadataResult Fail(string message)
        {
            return new CloudSaveMetadataResult(false, false, null, message);
        }
    }
}

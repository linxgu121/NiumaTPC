namespace NiumaCore.Save
{
    /// <summary>
    /// 存档同步状态。
    /// </summary>
    public enum SaveSyncState
    {
        /// <summary>
        /// 没有同步状态，通常表示尚未开始同步或不需要同步。
        /// </summary>
        None,

        /// <summary>
        /// 本地存档与云端存档已同步。
        /// </summary>
        Synced,

        /// <summary>
        /// 仅本地保存成功，本次流程没有进行云同步。
        /// </summary>
        LocalOnly,

        /// <summary>
        /// 本地有新存档等待上传到云端。
        /// </summary>
        PendingUpload,

        /// <summary>
        /// 云端有新存档等待下载到本地。
        /// </summary>
        PendingDownload,

        /// <summary>
        /// 本地和云端存档版本冲突，需要玩家或策略选择保留哪一份。
        /// </summary>
        Conflict,

        /// <summary>
        /// 同步失败，需要根据错误信息决定重试或提示玩家。
        /// </summary>
        Failed
    }
}

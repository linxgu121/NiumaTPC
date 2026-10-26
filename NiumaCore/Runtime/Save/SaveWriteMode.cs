namespace NiumaCore.Save
{
    /// <summary>
    /// 高层存档写入策略。
    /// </summary>
    public enum SaveWriteMode
    {
        /// <summary>
        /// 只写入本地，不主动上传云端。
        /// </summary>
        LocalOnly,

        /// <summary>
        /// 先写入本地，再尝试上传云端。
        /// 云端失败不应该破坏已经成功的本地存档。
        /// </summary>
        LocalThenCloud
    }
}

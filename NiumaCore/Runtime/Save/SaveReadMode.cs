namespace NiumaCore.Save
{
    /// <summary>
    /// 高层存档读取策略。
    /// </summary>
    public enum SaveReadMode
    {
        /// <summary>
        /// 只从本地读取存档。
        /// </summary>
        LocalOnly,

        /// <summary>
        /// 优先读取本地，本地不存在时再尝试读取云端。
        /// </summary>
        LocalFirst,

        /// <summary>
        /// 优先读取云端，云端失败时再尝试读取本地。
        /// </summary>
        CloudFirst
    }
}

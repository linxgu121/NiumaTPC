using System;

namespace NiumaCore.Save
{
    /// <summary>
    /// 云同步冲突信息。
    /// UI 可以根据本地和云端元数据让玩家选择保留哪一份。
    /// </summary>
    [Serializable]
    public sealed class SaveSyncConflict
    {
        public SaveSlotMetadata Local;
        public SaveSlotMetadata Remote;
        public string Message;
    }
}


using System;

namespace NiumaCore.Save
{
    /// <summary>
    /// 存档同步结果。
    /// </summary>
    [Serializable]
    public readonly struct SaveSyncResult
    {
        public readonly bool Succeeded;
        public readonly SaveSyncState State;
        public readonly SaveSlotMetadata Metadata;
        public readonly SaveSyncConflict Conflict;
        public readonly string Message;

        public SaveSyncResult(
            bool succeeded,
            SaveSyncState state,
            SaveSlotMetadata metadata,
            SaveSyncConflict conflict,
            string message)
        {
            Succeeded = succeeded;
            State = state;
            Metadata = metadata;
            Conflict = conflict;
            Message = message;
        }

        public static SaveSyncResult Success(SaveSlotMetadata metadata)
        {
            return new SaveSyncResult(true, SaveSyncState.Synced, metadata, null, null);
        }

        public static SaveSyncResult Fail(SaveSyncState state, string message)
        {
            return new SaveSyncResult(false, state, null, null, message);
        }

        public static SaveSyncResult ConflictDetected(SaveSyncConflict conflict)
        {
            return new SaveSyncResult(false, SaveSyncState.Conflict, null, conflict, conflict?.Message);
        }
    }
}


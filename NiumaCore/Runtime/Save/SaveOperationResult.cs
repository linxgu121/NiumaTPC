using System;

namespace NiumaCore.Save
{
    /// <summary>
    /// 高层存档写入结果。
    /// 用于 ISaveService.SaveAsync，表示本地保存和可选云同步后的最终结果。
    /// </summary>
    [Serializable]
    public readonly struct SaveOperationResult
    {
        /// <summary>
        /// 本次保存流程是否成功完成。
        /// </summary>
        public readonly bool Succeeded;

        /// <summary>
        /// 保存完成后的同步状态。
        /// </summary>
        public readonly SaveSyncState State;

        /// <summary>
        /// 保存完成后的存档元数据。
        /// </summary>
        public readonly SaveSlotMetadata Metadata;

        /// <summary>
        /// 上传云端时发现的冲突信息，没有冲突时为空。
        /// </summary>
        public readonly SaveSyncConflict Conflict;

        /// <summary>
        /// 失败或调试提示信息。
        /// </summary>
        public readonly string Message;

        public SaveOperationResult(
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

        public static SaveOperationResult Success(SaveSlotMetadata metadata, SaveSyncState state)
        {
            return new SaveOperationResult(true, state, metadata, null, null);
        }

        public static SaveOperationResult LocalSaved(SaveSlotMetadata metadata)
        {
            return new SaveOperationResult(true, SaveSyncState.LocalOnly, metadata, null, null);
        }

        public static SaveOperationResult CloudSynced(SaveSlotMetadata metadata)
        {
            return new SaveOperationResult(true, SaveSyncState.Synced, metadata, null, null);
        }

        public static SaveOperationResult Fail(SaveSyncState state, string message)
        {
            return new SaveOperationResult(false, state, null, null, message);
        }

        public static SaveOperationResult ConflictDetected(SaveSyncConflict conflict)
        {
            return new SaveOperationResult(false, SaveSyncState.Conflict, null, conflict, conflict?.Message);
        }
    }
}

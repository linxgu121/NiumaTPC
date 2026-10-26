using System.Threading;
using System.Threading.Tasks;

namespace NiumaCore.Save
{
    /// <summary>
    /// 高层存档服务接口。
    /// 普通业务模块只依赖这个协调入口，不直接关心本地存档、云存档和冲突处理的内部组合方式。
    /// </summary>
    public interface ISaveService
    {
        /// <summary>
        /// 保存指定载荷。
        /// 存档槽 ID 以 payload.Metadata.SlotId 为准，避免方法参数与元数据不一致。
        /// 是否只写本地、是否同时上传云端，由 writeMode 明确表达。
        /// </summary>
        Task<SaveOperationResult> SaveAsync(
            SavePayload payload,
            SaveWriteMode writeMode = SaveWriteMode.LocalOnly,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// 从指定存档槽加载存档。
        /// 读取本地、读取云端或本地优先等策略由 readMode 明确表达。
        /// </summary>
        Task<SaveLoadResult> LoadAsync(
            string slotId,
            SaveReadMode readMode = SaveReadMode.LocalFirst,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// 标记指定存档槽存在未保存变更。
        /// 如果该标记会影响崩溃恢复或自动保存，实现层应该将脏标记持久化。
        /// </summary>
        Task MarkDirtyAsync(string slotId, CancellationToken cancellationToken = default);

        /// <summary>
        /// 清除指定存档槽的脏标记。
        /// </summary>
        Task ClearDirtyAsync(string slotId, CancellationToken cancellationToken = default);

        /// <summary>
        /// 查询指定存档槽是否存在未保存变更。
        /// </summary>
        Task<bool> IsDirtyAsync(string slotId, CancellationToken cancellationToken = default);
    }
}

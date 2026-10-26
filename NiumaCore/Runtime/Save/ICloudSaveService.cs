using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace NiumaCore.Save
{
    /// <summary>
    /// 云存档服务。
    /// 具体实现可以是自建后端、平台云存档或 Mock 服务。
    /// </summary>
    public interface ICloudSaveService
    {
        /// <summary>
        /// 上传存档。
        /// 上传可能因为远端版本更新产生冲突，因此保留 SaveSyncResult。
        /// </summary>
        Task<SaveSyncResult> UploadAsync(SavePayload payload, CancellationToken cancellationToken = default);

        /// <summary>
        /// 下载指定云端存档。
        /// 下载只负责拿到远端载荷，本身不负责本地与远端冲突仲裁。
        /// </summary>
        Task<CloudSaveDownloadResult> DownloadAsync(string slotId, CancellationToken cancellationToken = default);

        /// <summary>
        /// 查询远端存档列表。
        /// </summary>
        Task<IReadOnlyList<SaveSlotMetadata>> ListRemoteSlotsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// 删除指定云端存档。
        /// 删除不产生本地与远端冲突，因此返回轻量操作结果。
        /// </summary>
        Task<CloudSaveDeleteResult> DeleteRemoteAsync(string slotId, CancellationToken cancellationToken = default);

        /// <summary>
        /// 查询指定云端存档的元数据。
        /// 纯云端接口只返回远端事实，不判断本地与远端是否同步。
        /// </summary>
        Task<CloudSaveMetadataResult> GetRemoteMetadataAsync(string slotId, CancellationToken cancellationToken = default);
    }
}

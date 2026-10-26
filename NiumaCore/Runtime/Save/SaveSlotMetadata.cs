using System;

namespace NiumaCore.Save
{
    /// <summary>
    /// 存档槽元数据。
    /// 用于展示存档列表、判断版本和处理云同步冲突。
    /// </summary>
    [Serializable]
    public sealed class SaveSlotMetadata
    {
        /// <summary>
        /// 存档槽唯一标识，例如 slot_01、auto_01。
        /// </summary>
        public string SlotId;

        /// <summary>
        /// 给玩家或调试面板展示的存档名称。
        /// </summary>
        public string DisplayName;

        /// <summary>
        /// 归属账号 ID，离线存档可以为空。
        /// </summary>
        public string UserId;

        /// <summary>
        /// 存档版本号，用于判断本地与远程谁更新。
        /// </summary>
        public long Revision;

        /// <summary>
        /// UTC Unix 秒级时间戳。
        /// 不直接序列化 DateTime，避免 Unity JsonUtility 在不同版本下行为不一致。
        /// </summary>
        public long UpdatedAtUnixSeconds;

        /// <summary>
        /// 当前存档累计游玩时长，单位为秒。
        /// </summary>
        public double PlayTimeSeconds;

        /// <summary>
        /// 存档内容校验值，用于发现传输损坏或重复内容。
        /// </summary>
        public string Checksum;

        /// <summary>
        /// 最后写入该存档的设备标识，用于云同步冲突提示。
        /// </summary>
        public string DeviceId;

        /// <summary>
        /// 运行期便捷访问属性，不参与 Unity 字段序列化。
        /// </summary>
        public DateTime UpdatedAtUtc => DateTimeOffset.FromUnixTimeSeconds(UpdatedAtUnixSeconds).UtcDateTime;
    }
}

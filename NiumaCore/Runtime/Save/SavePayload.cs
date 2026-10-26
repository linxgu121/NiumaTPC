using System;

namespace NiumaCore.Save
{
    /// <summary>
    /// 存档载荷。
    /// Core 层不关心具体序列化格式，业务模块可以把 JSON、二进制、压缩数据或加密数据放入 Data。
    /// </summary>
    [Serializable]
    public sealed class SavePayload
    {
        /// <summary>
        /// 存档槽元数据，用于展示、版本判断和云同步冲突处理。
        /// </summary>
        public SaveSlotMetadata Metadata;

        /// <summary>
        /// 序列化格式标识，例如 json、protobuf、binary、encrypted-json。
        /// 该字段只描述格式，不要求 Core 层理解内容。
        /// </summary>
        public string Format;

        /// <summary>
        /// 已序列化后的原始数据。
        /// 具体编码、压缩和加密策略由业务层或存档实现层决定。
        /// </summary>
        public byte[] Data;
    }
}

namespace NiumaHotUpdate
{
    /// <summary>
    /// 热更发布清单校验失败的稳定分类
    /// 启动流程通过错误码决定是否阻止进入游戏
    /// </summary>
    public enum HotUpdateManifestValidationErrorCode : byte
    {
        /// <summary>
        /// 清单校验成功
        /// </summary>
        None = 0,

        /// <summary>
        /// 未获得有效的热更发布清单
        /// </summary>
        ManifestMissing = 1,

        /// <summary>
        /// 清单没有声明适用的基础客户端版本
        /// </summary>
        BasePlayerVersionMissing = 2,

        /// <summary>
        /// 清单没有声明热更代码版本
        /// </summary>
        HotUpdateVersionMissing = 3,

        /// <summary>
        /// 清单没有声明目标运行平台
        /// </summary>
        PlatformMissing = 4,

        /// <summary>
        /// 清单要求的基础客户端版本与当前客户端不一致
        /// </summary>
        BasePlayerVersionMismatch = 5,

        /// <summary>
        /// 清单目标平台与当前客户端平台不一致
        /// </summary>
        PlatformMismatch = 6,

        /// <summary>
        /// 清单没有提供热更程序集地址列表
        /// 第一阶段至少需要一个热更程序集
        /// </summary>
        HotUpdateAssemblyListMissing = 7,

        /// <summary>
        /// 热更程序集列表包含空地址
        /// </summary>
        HotUpdateAssemblyAddressInvalid = 8,

        /// <summary>
        /// 清单没有提供 AOT 补充元数据列表
        /// 没有补充元数据时也必须使用空数组
        /// </summary>
        AotMetadataAssemblyListMissing = 9,

        /// <summary>
        /// AOT 补充元数据列表包含空地址
        /// </summary>
        AotMetadataAssemblyAddressInvalid = 10,

        /// <summary>
        /// 同一个 Addressables 地址在清单中出现多次
        /// </summary>
        DuplicateAssemblyAddress = 11,

        /// <summary>
        /// 清单没有声明入口程序集名称
        /// </summary>
        EntryAssemblyNameMissing = 12,

        /// <summary>
        /// 清单没有声明入口类型完整名称
        /// </summary>
        EntryTypeNameMissing = 13,

        /// <summary>
        /// 热更程序集列表包含空描述
        /// </summary>
        HotUpdateAssemblyDescriptorMissing = 14,

        /// <summary>
        /// 热更程序集 SHA256 缺失或格式无效
        /// </summary>
        HotUpdateAssemblyHashInvalid = 15
    }
}

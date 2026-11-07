namespace NiumaHotUpdate
{
    /// <summary>
    /// 热更运行环境初始化失败的稳定分类
    /// </summary>
    public enum HotUpdateInitializationErrorCode : byte
    {
        /// <summary>
        /// 热更初始化成功
        /// </summary>
        None = 0,

        /// <summary>
        /// 上一次初始化尚未结束
        /// </summary>
        InitializationInProgress = 1,

        /// <summary>
        /// 当前运行平台没有对应的热更发布内容
        /// </summary>
        UnsupportedPlatform = 2,

        /// <summary>
        /// 热更发布清单加载、解析或校验失败
        /// </summary>
        ManifestLoadFailed = 3,

        /// <summary>
        /// AOT 补充元数据资源加载失败
        /// </summary>
        AotMetadataResourceLoadFailed = 4,

        /// <summary>
        /// HybridCLR 拒绝加载 AOT 补充元数据
        /// </summary>
        AotMetadataRegistrationFailed = 5,

        /// <summary>
        /// 热更程序集资源加载失败
        /// </summary>
        HotUpdateAssemblyResourceLoadFailed = 6,

        /// <summary>
        /// 热更程序集字节无法加载为托管程序集
        /// </summary>
        HotUpdateAssemblyLoadFailed = 7,

        /// <summary>
        /// 多个热更资源生成了相同的程序集名称
        /// </summary>
        DuplicateHotUpdateAssemblyName = 8,

        /// <summary>
        /// 已加载程序集中不存在清单指定的入口程序集
        /// </summary>
        EntryAssemblyNotFound = 9,

        /// <summary>
        /// 入口程序集中不存在清单指定的入口类型
        /// </summary>
        EntryTypeNotFound = 10,

        /// <summary>
        /// 入口类型没有实现 IHotUpdateEntry
        /// </summary>
        EntryContractMismatch = 11,

        /// <summary>
        /// 无法创建热更入口实例
        /// </summary>
        EntryCreationFailed = 12,

        /// <summary>
        /// 热更入口执行初始化时发生异常
        /// </summary>
        EntryInitializationFailed = 13,

        /// <summary>
        /// 热更程序集内容与发布清单记录的 SHA256 不一致
        /// </summary>
        HotUpdateAssemblyHashMismatch = 14,

        /// <summary>
        /// 无法计算热更程序集 SHA256
        /// </summary>
        HotUpdateAssemblyHashCalculationFailed = 15


    }
}

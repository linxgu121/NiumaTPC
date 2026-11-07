namespace NiumaHotUpdate.Editor
{
    /// <summary>
    /// 热更发布前置校验错误
    /// </summary>
    internal enum HotUpdatePublishErrorCode
    {
        /// <summary>
        /// 校验通过
        /// </summary>
        None = 0,

        /// <summary>
        /// 发布上下文不存在
        /// </summary>
        InvalidContext = 1,

        /// <summary>
        /// Unity 正在播放、编译或刷新资源
        /// </summary>
        EditorBusy = 2,

        /// <summary>
        /// 当前构建平台不是第一阶段支持的 Windows 64 位平台
        /// </summary>
        UnsupportedBuildTarget = 3,

        /// <summary>
        /// 基础客户端版本为空
        /// </summary>
        BasePlayerVersionEmpty = 4,

        /// <summary>
        /// 基础客户端版本格式无效
        /// </summary>
        BasePlayerVersionInvalid = 5,

        /// <summary>
        /// 热更新版本为空
        /// </summary>
        HotUpdateVersionEmpty = 6,

        /// <summary>
        /// 热更新版本格式无效
        /// </summary>
        HotUpdateVersionInvalid = 7,

        /// <summary>
        /// 热更新版本没有高于基础客户端版本
        /// </summary>
        HotUpdateVersionNotNewer = 8,

        /// <summary>
        /// 输入的基础客户端版本与 PlayerSettings 不一致
        /// </summary>
        PlayerVersionMismatch = 9,

        /// <summary>
        /// HybridCLR 当前处于禁用状态
        /// </summary>
        HybridClrDisabled = 10,

        /// <summary>
        /// 热更程序集没有注册到 HybridCLR 配置
        /// </summary>
        HotUpdateAssemblyNotRegistered = 11,

        /// <summary>
        /// Addressables Settings 不存在
        /// </summary>
        AddressablesSettingsMissing = 12,

        /// <summary>
        /// 对应基础客户端的 Addressables 状态文件不存在
        /// </summary>
        ContentStateMissing = 13,

        /// <summary>
        /// 热更清单资源不存在
        /// </summary>
        ManifestAssetMissing = 14,

        /// <summary>
        /// 作为 Addressable 资源的 DLL Bytes 文件不存在
        /// </summary>
        DllBytesAssetMissing = 15,

        /// <summary>
        /// HybridCLR 编译热更程序集时发生异常
        /// </summary>
        HotUpdateCompilationFailed = 16,

        /// <summary>
        /// 编译完成后没有找到目标 DLL
        /// </summary>
        CompiledDllMissing = 17,

        /// <summary>
        /// 编译出的 DLL 内容为空
        /// </summary>
        CompiledDllEmpty = 18,

        /// <summary>
        /// 无法安全替换 Addressable DLL Bytes 文件
        /// </summary>
        DllBytesReplaceFailed = 19,

        /// <summary>
        /// 无法读取现有热更清单
        /// </summary>
        ManifestReadFailed = 20,

        /// <summary>
        /// 现有热更清单缺少必要数据
        /// </summary>
        ManifestInvalid = 21,

        /// <summary>
        /// 无法写入或导入新的热更清单
        /// </summary>
        ManifestWriteFailed = 22,

        /// <summary>
        /// 无法计算最终 DLL Bytes 的 SHA256
        /// </summary>
        DllHashCalculationFailed = 23,

        /// <summary>
        /// Addressables 增量构建没有返回有效结果
        /// </summary>
        AddressablesBuildReturnedNull = 24,

        /// <summary>
        /// Addressables 增量内容构建失败
        /// </summary>
        AddressablesContentUpdateFailed = 25,

        /// <summary>
        /// Addressables 完整内容构建失败
        /// </summary>
        AddressablesFullBuildFailed = 26,

        /// <summary>
        /// 完整构建完成后没有生成内容状态文件
        /// </summary>
        GeneratedContentStateMissing = 27,

        /// <summary>
        /// 无法把本次完整构建状态归档为基础版本
        /// </summary>
        ContentStateArchiveFailed = 28
    }
}

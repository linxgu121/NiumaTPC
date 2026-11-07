using System;

namespace NiumaHotUpdate
{
    /// <summary>
    /// 描述指定基础客户端可以加载的热更程序集及入口
    /// </summary>
    [Serializable]
    public sealed class HotUpdateManifest
    {
        /// <summary>
        /// 基础客户端版本
        /// </summary>
        public string BasePlayerVersion;
        /// <summary>
        /// 热更版本号
        /// </summary>
        public string HotUpdateVersion;
        /// <summary>
        /// 平台标识
        /// </summary>
        public string Platform;
        /// <summary>
        /// 必需的热更程序集
        /// </summary>
        public HotUpdateAssemblyDescriptor[] RequiredHotUpdateAssemblies;
        /// <summary>
        /// 必需的 AOT 元数据程序集
        /// </summary>
        public string[] RequiredAotMetadataAssemblies;
        /// <summary>
        /// 入口程序集名称
        /// 指定包含启动入口类的 DLL 名字
        /// </summary>
        public string EntryAssemblyName;
        /// <summary>
        /// 入口类型名称
        /// </summary>
        public string EntryTypeName;
    }
}

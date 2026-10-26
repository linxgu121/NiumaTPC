namespace NiumaResource
{
    /// <summary>
    /// 资源加载失败的稳定分类
    /// 业务层通过错误码决定提示和恢复方式
    /// </summary>
    public enum ResourceLoadErrorCode : byte
    {
        /// <summary>
        /// 加载成功，没有发生错误
        /// </summary>
        None = 0,

        /// <summary>
        /// 资源地址为空、只包含空格或不符合地址规则
        /// 请求不会提交给 Addressables
        /// </summary>
        InvalidAddress = 1,

        /// <summary>
        /// 地址指向的资源不能转换成调用方请求的资源类型
        /// 例如把音频资源按照 GameObject 请求
        /// </summary>
        InvalidAssetType = 2,

        /// <summary>
        /// Addressables 已接受请求但底层加载失败
        /// 可能由资源不存在、依赖缺失或 AssetBundle 损坏导致
        /// </summary>
        LoadFailed = 3,

        /// <summary>
        /// 资源服务尚未完成初始化
        /// 通常说明 Bootstrap 启动顺序不正确
        /// </summary>
        ServiceNotInitialized = 4,

        /// <summary>
        /// 资源服务正在关闭或已经关闭
        /// 此时拒绝新的资源加载请求
        /// </summary>
        ServiceShuttingDown = 5
    }
}
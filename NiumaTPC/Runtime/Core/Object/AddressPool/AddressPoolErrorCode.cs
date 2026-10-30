namespace NiumaTPC.Core.Object
{
    public enum AddressPoolErrorCode
    {
        /// <summary>没有错误</summary>
        None = 0,

        /// <summary>资源地址为空或格式无效</summary>
        InvalidAddress = 1,

        /// <summary>预热数量小于零</summary>
        InvalidPrewarmCount = 2,

        /// <summary>资源服务为空或尚未初始化</summary>
        ResourceServiceUnavailable = 3,

        /// <summary>对象池已销毁或正在关闭</summary>
        PoolSystemUnavailable = 4,

        /// <summary>指定地址池尚未准备完成</summary>
        PoolNotPrepared = 5,

        /// <summary>指定地址池正在等待释放</summary>
        PoolReleasing = 6,

        /// <summary>底层资源加载失败</summary>
        ResourceLoadFailed = 7,

        /// <summary>加载结果不是可用的 GameObject Prefab</summary>
        InvalidPrefab = 8,

        /// <summary>创建池实例时发生异常</summary>
        InstanceCreationFailed = 9
    }
}
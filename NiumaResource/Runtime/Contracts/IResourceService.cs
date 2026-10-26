using System.Threading.Tasks;

namespace NiumaResource
{
    /// <summary>
    /// 业务模块使用的统一资源加载入口
    /// 调用方不直接依赖 Addressables 或底层操作句柄
    /// </summary>
    public interface IResourceService
    {
        /// <summary>
        /// 资源服务是否已经完成初始化并允许接收加载请求
        /// 服务关闭后会重新变为 false
        /// </summary>
        bool IsInitialized { get; }

        /// <summary>
        /// 按稳定地址异步获取指定类型的 Unity 资源
        /// 方法会检查服务状态、资源地址和加载结果
        /// 成功后调用方负责释放返回的 Lease
        /// </summary>
        Task<ResourceLoadResult<T>> LoadAsync<T>(string address) where T : UnityEngine.Object;

        /// <summary>
        /// 停止接受新请求并释放服务持有的全部底层资源
        /// 重复调用必须保持安全
        /// </summary>
        void Shutdown();
    }
}
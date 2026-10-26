using System.Threading;
using System.Threading.Tasks;

namespace NiumaCore.Network
{
    /// <summary>
    /// 可插拔网络客户端。
    /// Save、Auth 等模块只依赖该接口，不直接依赖 UnityWebRequest 或具体平台 SDK。
    /// </summary>
    public interface INetworkClient
    {
        Task<NetworkResponse<TResponse>> SendAsync<TRequest, TResponse>(
            NetworkRequest<TRequest> request,
            CancellationToken cancellationToken = default);
    }
}


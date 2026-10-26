using System;
using System.Collections.Generic;

namespace NiumaCore.Network
{
    /// <summary>
    /// 网络请求描述。
    /// Body 使用泛型承载，序列化方式由具体 INetworkClient 实现决定。
    /// </summary>
    [Serializable]
    public sealed class NetworkRequest<TBody>
    {
        public NetworkHttpMethod Method { get; }
        public string Path { get; }
        public TBody Body { get; }
        public IReadOnlyList<NetworkHeader> Headers { get; }
        public bool RequiresAuth { get; }
        public float TimeoutSeconds { get; }

        public NetworkRequest(
            NetworkHttpMethod method,
            string path,
            TBody body = default,
            IReadOnlyList<NetworkHeader> headers = null,
            bool requiresAuth = true,
            float timeoutSeconds = 15f)
        {
            Method = method;
            Path = path;
            Body = body;
            Headers = headers ?? Array.Empty<NetworkHeader>();
            RequiresAuth = requiresAuth;
            TimeoutSeconds = timeoutSeconds;
        }
    }
}


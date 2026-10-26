using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NiumaCore.Auth;
using UnityEngine.Networking;

namespace NiumaCore.Network
{
    /// <summary>
    /// 基于 UnityWebRequest 的通用 JSON 网络客户端。
    /// 负责 URL、HTTP、超时、取消、JSON 和 Bearer Token，
    /// 不解析仓库、登录等具体业务响应。
    /// </summary>
    public sealed class UnityWebRequestNetworkClient :
        INetworkClient
    {
        #region Dependencies

        private readonly Uri _baseUri;
        private readonly IAccessTokenProvider
            _accessTokenProvider;

        #endregion

        #region Public Properties

        public string BaseUrl =>
            _baseUri.AbsoluteUri.TrimEnd('/');

        #endregion

        public UnityWebRequestNetworkClient(
            string baseUrl,
            IAccessTokenProvider accessTokenProvider = null)
        {
            if (!TryNormalizeBaseUri(
                    baseUrl,
                    out _baseUri,
                    out var error))
            {
                throw new ArgumentException(
                    error,
                    nameof(baseUrl));
            }

            _accessTokenProvider = accessTokenProvider;
        }

        #region INetworkClient

        public async Task<NetworkResponse<TResponse>>
            SendAsync<TRequest, TResponse>(
                NetworkRequest<TRequest> request,
                CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (request == null)
            {
                return MakeFailure<TResponse>(
                    NetworkErrorCode.Unknown,
                    "网络请求不能为空");
            }

            if (!TryBuildRequestUri(
                    request.Path,
                    out var requestUri,
                    out var uriError))
            {
                return MakeFailure<TResponse>(
                    NetworkErrorCode.Unknown,
                    uriError);
            }

            if (!TryGetHttpMethod(
                    request.Method,
                    out var httpMethod))
            {
                return MakeFailure<TResponse>(
                    NetworkErrorCode.Unknown,
                    "网络请求包含不支持的 HTTP 方法");
            }

            if (!TryGetTimeoutSeconds(
                    request.TimeoutSeconds,
                    out var timeoutSeconds,
                    out var timeoutError))
            {
                return MakeFailure<TResponse>(
                    NetworkErrorCode.Unknown,
                    timeoutError);
            }

            byte[] requestBody = null;

            if (!ReferenceEquals(request.Body, null) &&
                !TrySerializeRequest(
                    request.Body,
                    out requestBody,
                    out var serializationError))
            {
                return MakeFailure<TResponse>(
                    NetworkErrorCode.SerializationFailed,
                    serializationError);
            }

            using var webRequest =
                new UnityWebRequest(
                    requestUri.AbsoluteUri,
                    httpMethod)
                {
                    downloadHandler =
                        new DownloadHandlerBuffer(),
                    timeout = timeoutSeconds
                };

            if (requestBody != null)
            {
                webRequest.uploadHandler =
                    new UploadHandlerRaw(requestBody);

                webRequest.SetRequestHeader(
                    "Content-Type",
                    "application/json; charset=utf-8");
            }

            webRequest.SetRequestHeader(
                "Accept",
                "application/json");

            if (!TryApplyCustomHeaders(
                    webRequest,
                    request.Headers,
                    out var headerError))
            {
                return MakeFailure<TResponse>(
                    NetworkErrorCode.Unknown,
                    headerError);
            }

            if (request.RequiresAuth)
            {
                if (_accessTokenProvider == null ||
                    !_accessTokenProvider.TryGetAccessToken(
                        out var accessToken) ||
                    string.IsNullOrWhiteSpace(accessToken))
                {
                    return MakeFailure<TResponse>(
                        NetworkErrorCode.Unauthorized,
                        "当前请求需要登录，但没有可用 Access Token");
                }

                // 最后设置认证头，调用方不能通过自定义 Header 覆盖它。
                webRequest.SetRequestHeader(
                    "Authorization",
                    $"Bearer {accessToken.Trim()}");
            }

            try
            {
                await SendRequestAsync(
                    webRequest,
                    cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception)
            {
                return MakeFailure<TResponse>(
                    NetworkErrorCode.NetworkUnavailable,
                    "网络请求无法发送");
            }

            return MapCompletedResponse<TResponse>(
                webRequest);
        }

        #endregion

        #region Request Construction

        private static bool TryNormalizeBaseUri(
            string baseUrl,
            out Uri baseUri,
            out string error)
        {
            baseUri = null;

            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                error = "服务器 BaseUrl 不能为空";
                return false;
            }

            if (!Uri.TryCreate(
                    baseUrl.Trim(),
                    UriKind.Absolute,
                    out var candidate))
            {
                error = "服务器 BaseUrl 不是合法绝对地址";
                return false;
            }

            if (candidate.Scheme != Uri.UriSchemeHttp &&
                candidate.Scheme != Uri.UriSchemeHttps)
            {
                error = "服务器 BaseUrl 只允许 HTTP 或 HTTPS";
                return false;
            }

            if (!string.IsNullOrEmpty(candidate.Query) ||
                !string.IsNullOrEmpty(candidate.Fragment))
            {
                error = "服务器 BaseUrl 不能包含查询参数或片段";
                return false;
            }

            var normalized = candidate.AbsoluteUri;

            if (!normalized.EndsWith(
                    "/",
                    StringComparison.Ordinal))
            {
                normalized += "/";
            }

            baseUri = new Uri(
                normalized,
                UriKind.Absolute);

            error = string.Empty;
            return true;
        }

        private bool TryBuildRequestUri(
            string path,
            out Uri requestUri,
            out string error)
        {
            requestUri = null;

            if (string.IsNullOrWhiteSpace(path))
            {
                error = "网络请求 Path 不能为空";
                return false;
            }

            var normalizedPath = path.Trim();

            // 禁止业务模块传入完整 URL，避免认证 Token 被发送到其他主机。
            if (Uri.TryCreate(
                    normalizedPath,
                    UriKind.Absolute,
                    out _))
            {
                error = "网络请求 Path 必须是相对路径";
                return false;
            }

            normalizedPath =
                normalizedPath.TrimStart('/');

            if (normalizedPath.Length == 0)
            {
                error = "网络请求 Path 不能只包含斜杠";
                return false;
            }

            requestUri = new Uri(
                _baseUri,
                normalizedPath);

            error = string.Empty;
            return true;
        }

        private static bool TryGetHttpMethod(
            NetworkHttpMethod method,
            out string httpMethod)
        {
            switch (method)
            {
                case NetworkHttpMethod.Get:
                    httpMethod = "GET";
                    return true;

                case NetworkHttpMethod.Post:
                    httpMethod = "POST";
                    return true;

                case NetworkHttpMethod.Put:
                    httpMethod = "PUT";
                    return true;

                case NetworkHttpMethod.Patch:
                    httpMethod = "PATCH";
                    return true;

                case NetworkHttpMethod.Delete:
                    httpMethod = "DELETE";
                    return true;

                default:
                    httpMethod = string.Empty;
                    return false;
            }
        }

        private static bool TryGetTimeoutSeconds(
            float configuredSeconds,
            out int timeoutSeconds,
            out string error)
        {
            timeoutSeconds = 0;

            if (float.IsNaN(configuredSeconds) ||
                float.IsInfinity(configuredSeconds) ||
                configuredSeconds <= 0f ||
                configuredSeconds > int.MaxValue)
            {
                error = "网络请求超时时间必须是有效正数";
                return false;
            }

            // UnityWebRequest 只接受整数秒，向上取整避免提前超时。
            timeoutSeconds = Math.Max(
                1,
                (int)Math.Ceiling(configuredSeconds));

            error = string.Empty;
            return true;
        }

        private static bool TryApplyCustomHeaders(
            UnityWebRequest webRequest,
            System.Collections.Generic
                .IReadOnlyList<NetworkHeader> headers,
            out string error)
        {
            if (headers == null)
            {
                error = string.Empty;
                return true;
            }

            foreach (var header in headers)
            {
                var key = header.Key?.Trim();
                var value = header.Value?.Trim();

                if (string.IsNullOrEmpty(key) ||
                    string.IsNullOrEmpty(value))
                {
                    error = "网络请求 Header 的名称和值不能为空";
                    return false;
                }

                if (key.IndexOf('\r') >= 0 ||
                    key.IndexOf('\n') >= 0 ||
                    value.IndexOf('\r') >= 0 ||
                    value.IndexOf('\n') >= 0)
                {
                    error = "网络请求 Header 不能包含换行符";
                    return false;
                }

                if (string.Equals(
                        key,
                        "Authorization",
                        StringComparison.OrdinalIgnoreCase))
                {
                    error =
                        "业务请求不能自行覆盖 Authorization Header";
                    return false;
                }

                try
                {
                    webRequest.SetRequestHeader(
                        key,
                        value);
                }
                catch (Exception)
                {
                    error =
                        $"网络请求 Header 不受支持：{key}";
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }

        #endregion

        #region Request Execution

        private static async Task SendRequestAsync(
            UnityWebRequest webRequest,
            CancellationToken cancellationToken)
        {
            var operation =
                webRequest.SendWebRequest();

            while (!operation.isDone)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    webRequest.Abort();
                    cancellationToken
                        .ThrowIfCancellationRequested();
                }

                // 返回 Unity 同步上下文，下一帧继续检查。
                await Task.Yield();
            }

            cancellationToken.ThrowIfCancellationRequested();
        }

        #endregion

        #region Response Mapping

        private static NetworkResponse<TResponse>
            MapCompletedResponse<TResponse>(
                UnityWebRequest webRequest)
        {
            var statusCode =
                webRequest.responseCode > int.MaxValue
                    ? int.MaxValue
                    : (int)webRequest.responseCode;

            var rawText =
                webRequest.downloadHandler?.text ??
                string.Empty;

            if (webRequest.result ==
                UnityWebRequest.Result.ConnectionError)
            {
                var errorCode =
                    IsTimeoutMessage(webRequest.error)
                        ? NetworkErrorCode.Timeout
                        : NetworkErrorCode.NetworkUnavailable;

                return MakeFailure<TResponse>(
                    errorCode,
                    string.IsNullOrWhiteSpace(webRequest.error)
                        ? "无法连接服务器"
                        : webRequest.error,
                    statusCode,
                    rawText);
            }

            if (webRequest.result == UnityWebRequest.Result.DataProcessingError)
            {
                return MakeFailure<TResponse>(
                    NetworkErrorCode.SerializationFailed,
                    string.IsNullOrWhiteSpace(webRequest.error)
                        ? "无法读取服务器响应"
                        : webRequest.error,
                    statusCode,
                    rawText);
            }

            if (statusCode < 200 ||
                statusCode > 299 ||
                webRequest.result ==
                UnityWebRequest.Result.ProtocolError)
            {
                return MakeFailure<TResponse>(
                    MapHttpError(statusCode),
                    string.IsNullOrWhiteSpace(webRequest.error)
                        ? $"HTTP 请求失败：{statusCode}"
                        : webRequest.error,
                    statusCode,
                    rawText);
            }

            if (!TryDeserializeResponse(
                    rawText,
                    out TResponse responseData,
                    out var deserializationError))
            {
                return MakeFailure<TResponse>(
                    NetworkErrorCode.SerializationFailed,
                    deserializationError,
                    statusCode,
                    rawText);
            }

            return NetworkResponse<TResponse>.Success(
                responseData,
                statusCode,
                rawText);
        }

        private static NetworkErrorCode MapHttpError(
            int statusCode)
        {
            switch (statusCode)
            {
                case 401:
                    return NetworkErrorCode.Unauthorized;

                case 403:
                    return NetworkErrorCode.Forbidden;

                case 404:
                    return NetworkErrorCode.NotFound;

                case 409:
                    return NetworkErrorCode.Conflict;

                default:
                    return statusCode >= 500
                        ? NetworkErrorCode.ServerError
                        : NetworkErrorCode.Unknown;
            }
        }

        private static bool IsTimeoutMessage(
            string message)
        {
            return !string.IsNullOrWhiteSpace(message) &&
                   (message.IndexOf(
                        "timeout",
                        StringComparison.OrdinalIgnoreCase) >= 0 ||
                    message.IndexOf(
                        "timed out",
                        StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static NetworkResponse<TResponse>
            MakeFailure<TResponse>(
                NetworkErrorCode errorCode,
                string message,
                int statusCode = 0,
                string rawText = null)
        {
            return NetworkResponse<TResponse>.Fail(
                errorCode,
                message,
                statusCode,
                rawText);
        }

        #endregion

        #region JSON

        private static bool TrySerializeRequest<TRequest>(
            TRequest body,
            out byte[] bytes,
            out string error)
        {
            bytes = null;

            try
            {
                using var stream = new MemoryStream();

                var serializer =
                    new DataContractJsonSerializer(
                        typeof(TRequest));

                serializer.WriteObject(
                    stream,
                    body);

                bytes = stream.ToArray();
                error = string.Empty;
                return true;
            }
            catch (SerializationException)
            {
                error = "网络请求 JSON 序列化失败";
                return false;
            }
            catch (InvalidDataContractException)
            {
                error = "网络请求 DTO 不符合 JSON 数据契约";
                return false;
            }
            catch (Exception)
            {
                error = "网络请求 DTO 无法序列化";
                return false;
            }
        }

        private static bool TryDeserializeResponse<TResponse>(
            string json,
            out TResponse response,
            out string error)
        {
            response = default;

            if (typeof(TResponse) == typeof(string))
            {
                response = (TResponse)(object)(
                    json ?? string.Empty);

                error = string.Empty;
                return true;
            }

            // object 用于不关心响应数据的成功请求。
            if (typeof(TResponse) == typeof(object) &&
                string.IsNullOrWhiteSpace(json))
            {
                error = string.Empty;
                return true;
            }

            if (string.IsNullOrWhiteSpace(json))
            {
                error = "服务器成功响应缺少 JSON 正文";
                return false;
            }

            try
            {
                var bytes =
                    Encoding.UTF8.GetBytes(json);

                using var stream =
                    new MemoryStream(
                        bytes,
                        writable: false);

                var serializer =
                    new DataContractJsonSerializer(
                        typeof(TResponse));

                var candidate =
                    serializer.ReadObject(stream);

                if (candidate is not TResponse typed)
                {
                    error = "服务器响应 JSON 类型不符合请求契约";
                    return false;
                }

                response = typed;
                error = string.Empty;
                return true;
            }
            catch (SerializationException)
            {
                error = "服务器响应 JSON 反序列化失败";
                return false;
            }
            catch (InvalidDataContractException)
            {
                error = "响应 DTO 不符合 JSON 数据契约";
                return false;
            }
            catch (Exception)
            {
                error = "服务器响应 JSON 无法读取";
                return false;
            }
        }

        #endregion
    }
}
using System;

namespace NiumaCore.Network
{
    /// <summary>
    /// 网络响应描述。
    /// Data 为反序列化后的业务数据，RawText 保留原始响应便于调试。
    /// </summary>
    [Serializable]
    public readonly struct NetworkResponse<TData>
    {
        public readonly bool Succeeded;
        public readonly int StatusCode;
        public readonly NetworkErrorCode ErrorCode;
        public readonly string Message;
        public readonly TData Data;
        public readonly string RawText;

        public NetworkResponse(
            bool succeeded,
            int statusCode,
            NetworkErrorCode errorCode,
            string message,
            TData data,
            string rawText = null)
        {
            Succeeded = succeeded;
            StatusCode = statusCode;
            ErrorCode = errorCode;
            Message = message;
            Data = data;
            RawText = rawText;
        }

        public static NetworkResponse<TData> Success(TData data, int statusCode = 200, string rawText = null)
        {
            return new NetworkResponse<TData>(true, statusCode, NetworkErrorCode.None, null, data, rawText);
        }

        public static NetworkResponse<TData> Fail(
            NetworkErrorCode errorCode,
            string message,
            int statusCode = 0,
            string rawText = null)
        {
            return new NetworkResponse<TData>(false, statusCode, errorCode, message, default, rawText);
        }
    }
}


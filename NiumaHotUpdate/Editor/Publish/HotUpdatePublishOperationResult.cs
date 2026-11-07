namespace NiumaHotUpdate.Editor
{
    /// <summary>
    /// 热更发布写入操作结果
    /// </summary>
    internal readonly struct HotUpdatePublishOperationResult
    {
        public bool Succeeded =>
            ErrorCode == HotUpdatePublishErrorCode.None;

        public HotUpdatePublishErrorCode ErrorCode { get; }
        public string Message { get; }

        private HotUpdatePublishOperationResult(
            HotUpdatePublishErrorCode errorCode,
            string message)
        {
            ErrorCode = errorCode;
            Message = message;
        }

        public static HotUpdatePublishOperationResult Success(
            string message)
        {
            return new HotUpdatePublishOperationResult(
                HotUpdatePublishErrorCode.None,
                message);
        }

        public static HotUpdatePublishOperationResult Failure(
            HotUpdatePublishErrorCode errorCode,
            string message)
        {
            return new HotUpdatePublishOperationResult(
                errorCode,
                message);
        }
    }
}

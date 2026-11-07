namespace NiumaHotUpdate.Editor
{
    /// <summary>
    /// 热更发布前置校验结果
    /// </summary>
    internal readonly struct HotUpdatePublishValidationResult
    {
        public bool Succeeded => ErrorCode == HotUpdatePublishErrorCode.None;
        public HotUpdatePublishErrorCode ErrorCode { get; }
        public string Message { get; }

        private HotUpdatePublishValidationResult(
            HotUpdatePublishErrorCode errorCode,
            string message)
        {
            ErrorCode = errorCode;
            Message = message;
        }

        public static HotUpdatePublishValidationResult Success()
        {
            return new HotUpdatePublishValidationResult(
                HotUpdatePublishErrorCode.None,
                "热更发布前置校验通过");
        }

        public static HotUpdatePublishValidationResult Failure(
            HotUpdatePublishErrorCode errorCode,
            string message)
        {
            return new HotUpdatePublishValidationResult(errorCode, message);
        }
    }
}

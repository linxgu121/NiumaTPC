using System;

namespace NiumaHotUpdate
{
    /// <summary>
    /// 热更运行环境初始化的最终结果
    /// </summary>
    public readonly struct HotUpdateInitializationResult
    {
        #region Properties(结果属性)

        public bool Succeeded { get; }

        public HotUpdateInitializationErrorCode ErrorCode { get; }

        public string ErrorMessage { get; }

        public HotUpdateManifest Manifest { get; }

        public IHotUpdateEntry Entry { get; }

        public string HotUpdateVersion => Manifest?.HotUpdateVersion ?? string.Empty;

        #endregion

        #region Constructor(构造方法)

        private HotUpdateInitializationResult(
            bool succeeded,
            HotUpdateInitializationErrorCode errorCode,
            string errorMessage,
            HotUpdateManifest manifest,
            IHotUpdateEntry entry)
        {
            Succeeded = succeeded;
            ErrorCode = errorCode;
            ErrorMessage = errorMessage ?? string.Empty;
            Manifest = manifest;
            Entry = entry;
        }

        #endregion

        #region Factory(结果创建)

        /// <summary>
        /// 使用已经完成初始化的清单和入口创建成功结果
        /// </summary>
        public static HotUpdateInitializationResult Success(HotUpdateManifest manifest, IHotUpdateEntry entry)
        {
            if (manifest == null)
            {
                throw new ArgumentNullException(nameof(manifest));
            }

            if (entry == null)
            {
                throw new ArgumentNullException(nameof(entry));
            }

            return new HotUpdateInitializationResult(
                true,
                HotUpdateInitializationErrorCode.None,
                string.Empty,
                manifest,
                entry);
        }

        /// <summary>
        /// 使用非 None 错误码创建初始化失败结果
        /// </summary>
        public static HotUpdateInitializationResult Failure(HotUpdateInitializationErrorCode errorCode, string errorMessage)
        {
            if (errorCode == HotUpdateInitializationErrorCode.None)
            {
                throw new ArgumentOutOfRangeException(nameof(errorCode), "热更初始化失败结果不能使用 None 错误码");
            }

            return new HotUpdateInitializationResult(false, errorCode, errorMessage, null, null);
        }

        #endregion
    }
}

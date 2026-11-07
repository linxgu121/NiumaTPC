using System;

namespace NiumaHotUpdate
{
    /// <summary>
    /// 热更发布清单兼容性校验的最终结果
    /// </summary>
    public readonly struct HotUpdateManifestValidationResult
    {
        #region Properties(结果属性)

        public bool Succeeded { get; }

        public HotUpdateManifestValidationErrorCode ErrorCode { get; }

        public string ErrorMessage { get; }

        #endregion

        #region Constructor(构造方法)

        private HotUpdateManifestValidationResult(bool succeeded, HotUpdateManifestValidationErrorCode errorCode, string errorMessage)
        {
            Succeeded = succeeded;
            ErrorCode = errorCode;
            ErrorMessage = errorMessage ?? string.Empty;
        }

        #endregion

        #region Factory(结果创建)

        /// <summary>
        /// 创建清单兼容性校验成功结果
        /// </summary>
        public static HotUpdateManifestValidationResult Success()
        {
            return new HotUpdateManifestValidationResult(true, HotUpdateManifestValidationErrorCode.None, string.Empty);
        }

        /// <summary>
        /// 使用非 None 错误码创建清单兼容性校验失败结果
        /// </summary>
        public static HotUpdateManifestValidationResult Failure(HotUpdateManifestValidationErrorCode errorCode, string errorMessage)
        {
            if (errorCode == HotUpdateManifestValidationErrorCode.None)
            {
                throw new ArgumentOutOfRangeException(nameof(errorCode), "清单校验失败结果不能使用 None 错误码");
            }

            return new HotUpdateManifestValidationResult(false, errorCode, errorMessage);
        }

        #endregion
    }
}

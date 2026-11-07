using System;
using NiumaResource;

namespace NiumaHotUpdate
{
    /// <summary>
    /// 热更发布清单加载、解析和兼容性校验的最终结果
    /// </summary>
    public readonly struct HotUpdateManifestLoadResult
    {
        #region Properties(结果属性)

        public bool Succeeded { get; }

        public HotUpdateManifestLoadErrorCode ErrorCode { get; }

        public ResourceLoadErrorCode ResourceErrorCode { get; }

        public HotUpdateManifestValidationErrorCode ValidationErrorCode { get; }

        public string ErrorMessage { get; }

        public HotUpdateManifest Manifest { get; }

        #endregion

        #region Constructor(构造方法)

        private HotUpdateManifestLoadResult(
            bool succeeded,
            HotUpdateManifestLoadErrorCode errorCode,
            ResourceLoadErrorCode resourceErrorCode,
            HotUpdateManifestValidationErrorCode validationErrorCode,
            string errorMessage,
            HotUpdateManifest manifest)
        {
            Succeeded = succeeded;
            ErrorCode = errorCode;
            ResourceErrorCode = resourceErrorCode;
            ValidationErrorCode = validationErrorCode;
            ErrorMessage = errorMessage ?? string.Empty;
            Manifest = manifest;
        }

        #endregion

        #region Factory(结果创建)

        /// <summary>
        /// 使用已经完成兼容性校验的清单创建成功结果
        /// </summary>
        public static HotUpdateManifestLoadResult Success(HotUpdateManifest manifest)
        {
            if (manifest == null)
            {
                throw new ArgumentNullException(nameof(manifest));
            }

            return new HotUpdateManifestLoadResult(
                true,
                HotUpdateManifestLoadErrorCode.None,
                ResourceLoadErrorCode.None,
                HotUpdateManifestValidationErrorCode.None,
                string.Empty,
                manifest);
        }

        /// <summary>
        /// 使用稳定错误码和底层诊断信息创建清单加载失败结果
        /// </summary>
        public static HotUpdateManifestLoadResult Failure(
            HotUpdateManifestLoadErrorCode errorCode,
            string errorMessage,
            ResourceLoadErrorCode resourceErrorCode = ResourceLoadErrorCode.None,
            HotUpdateManifestValidationErrorCode validationErrorCode = HotUpdateManifestValidationErrorCode.None)
        {
            if (errorCode == HotUpdateManifestLoadErrorCode.None)
            {
                throw new ArgumentOutOfRangeException(nameof(errorCode), "清单加载失败结果不能使用 None 错误码");
            }

            return new HotUpdateManifestLoadResult(
                false,
                errorCode,
                resourceErrorCode,
                validationErrorCode,
                errorMessage,
                null);
        }

        #endregion
    }
}

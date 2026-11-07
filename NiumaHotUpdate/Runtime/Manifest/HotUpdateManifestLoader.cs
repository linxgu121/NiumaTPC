using System;
using System.Threading.Tasks;
using NiumaResource;
using UnityEngine;

namespace NiumaHotUpdate
{
    /// <summary>
    /// 通过资源服务加载、解析并校验远程热更发布清单
    /// </summary>
    public sealed class HotUpdateManifestLoader
    {
        #region Constants(常量)

        public const string DefaultManifestAddress = "hotupdate/windows/manifest";

        #endregion

        #region Dependencies(依赖)

        private readonly IResourceService _resourceService;

        #endregion

        #region Constructor(构造方法)

        public HotUpdateManifestLoader(IResourceService resourceService)
        {
            _resourceService = resourceService ?? throw new ArgumentNullException(nameof(resourceService));
        }

        #endregion

        #region Public API(公共接口)

        /// <summary>
        /// 加载远程清单并校验其是否适用于当前基础客户端和运行平台
        /// </summary>
        public async Task<HotUpdateManifestLoadResult> LoadAsync(
            string currentBasePlayerVersion,
            string currentPlatform,
            string manifestAddress = DefaultManifestAddress)
        {
            ResourceLoadResult<TextAsset> resourceResult = await _resourceService.LoadAsync<TextAsset>(manifestAddress);

            if (!resourceResult.Succeeded)
            {
                return HotUpdateManifestLoadResult.Failure(
                    HotUpdateManifestLoadErrorCode.ResourceLoadFailed,
                    $"热更发布清单资源加载失败，Address={manifestAddress}, Detail={resourceResult.ErrorMessage}",
                    resourceResult.ErrorCode);
            }

            using (IResourceLease<TextAsset> lease = resourceResult.Lease)
            {
                string json = lease.Asset.text;

                if (string.IsNullOrWhiteSpace(json))
                {
                    return HotUpdateManifestLoadResult.Failure(
                        HotUpdateManifestLoadErrorCode.ManifestTextEmpty,
                        $"热更发布清单文本为空，Address={manifestAddress}");
                }

                HotUpdateManifest manifest;

                try
                {
                    manifest = JsonUtility.FromJson<HotUpdateManifest>(json);
                }
                catch (Exception exception)
                {
                    return HotUpdateManifestLoadResult.Failure(
                        HotUpdateManifestLoadErrorCode.ManifestJsonInvalid,
                        $"热更发布清单 JSON 解析失败，Address={manifestAddress}, Error={exception.Message}");
                }

                if (manifest == null)
                {
                    return HotUpdateManifestLoadResult.Failure(
                        HotUpdateManifestLoadErrorCode.ManifestJsonInvalid,
                        $"热更发布清单 JSON 没有生成有效对象，Address={manifestAddress}");
                }

                HotUpdateManifestValidationResult validationResult =
                    HotUpdateManifestValidator.Validate(manifest, currentBasePlayerVersion, currentPlatform);

                if (!validationResult.Succeeded)
                {
                    return HotUpdateManifestLoadResult.Failure(
                        HotUpdateManifestLoadErrorCode.ManifestValidationFailed,
                        validationResult.ErrorMessage,
                        validationErrorCode: validationResult.ErrorCode);
                }

                return HotUpdateManifestLoadResult.Success(manifest);
            }
        }

        #endregion
    }
}

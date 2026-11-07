using System;
using System.IO;
using System.Linq;
using HybridCLR;
using UnityEditor;
using UnityEditor.AddressableAssets;

namespace NiumaHotUpdate.Editor
{
    /// <summary>
    /// 在写入资源或执行构建前检查热更发布环境
    /// </summary>
    internal static class HotUpdatePublishValidator
    {
        /// <summary>
        /// 校验版本、平台、HybridCLR、Addressables 和基础状态文件是否满足增量发布要求
        /// </summary>
        public static HotUpdatePublishValidationResult Validate(
            HotUpdatePublishContext context)
        {
            if (context == null)
            {
                return Failure(
                    HotUpdatePublishErrorCode.InvalidContext,
                    "热更发布上下文不存在");
            }

            if (EditorApplication.isPlayingOrWillChangePlaymode ||
                EditorApplication.isCompiling ||
                EditorApplication.isUpdating)
            {
                return Failure(
                    HotUpdatePublishErrorCode.EditorBusy,
                    "Unity 正在播放、编译或刷新资源，请等待编辑器空闲后再发布");
            }

            if (context.Target != BuildTarget.StandaloneWindows64)
            {
                return Failure(
                    HotUpdatePublishErrorCode.UnsupportedBuildTarget,
                    $"第一阶段仅支持 StandaloneWindows64，当前平台={context.Target}");
            }

            if (string.IsNullOrWhiteSpace(context.BasePlayerVersion))
            {
                return Failure(
                    HotUpdatePublishErrorCode.BasePlayerVersionEmpty,
                    "基础客户端版本不能为空");
            }

            if (!Version.TryParse(
                    context.BasePlayerVersion,
                    out Version basePlayerVersion))
            {
                return Failure(
                    HotUpdatePublishErrorCode.BasePlayerVersionInvalid,
                    $"基础客户端版本格式无效，Version={context.BasePlayerVersion}");
            }

            if (string.IsNullOrWhiteSpace(context.HotUpdateVersion))
            {
                return Failure(
                    HotUpdatePublishErrorCode.HotUpdateVersionEmpty,
                    "热更新版本不能为空");
            }

            if (!Version.TryParse(
                    context.HotUpdateVersion,
                    out Version hotUpdateVersion))
            {
                return Failure(
                    HotUpdatePublishErrorCode.HotUpdateVersionInvalid,
                    $"热更新版本格式无效，Version={context.HotUpdateVersion}");
            }

            if (hotUpdateVersion.CompareTo(basePlayerVersion) <= 0)
            {
                return Failure(
                    HotUpdatePublishErrorCode.HotUpdateVersionNotNewer,
                    $"热更新版本必须高于基础客户端版本，Base={basePlayerVersion}，HotUpdate={hotUpdateVersion}");
            }

            if (!string.Equals(
                    PlayerSettings.bundleVersion,
                    context.BasePlayerVersion,
                    StringComparison.Ordinal))
            {
                return Failure(
                    HotUpdatePublishErrorCode.PlayerVersionMismatch,
                    $"基础客户端版本与 PlayerSettings 不一致，Input={context.BasePlayerVersion}，PlayerSettings={PlayerSettings.bundleVersion}");
            }

            if (!SettingsUtil.Enable)
            {
                return Failure(
                    HotUpdatePublishErrorCode.HybridClrDisabled,
                    "HybridCLR 当前处于禁用状态");
            }

            bool assemblyRegistered =
                SettingsUtil.HotUpdateAssemblyNamesExcludePreserved.Contains(
                    HotUpdatePublishContext.HotUpdateAssemblyName);

            if (!assemblyRegistered)
            {
                return Failure(
                    HotUpdatePublishErrorCode.HotUpdateAssemblyNotRegistered,
                    $"HybridCLR 没有注册热更程序集 {HotUpdatePublishContext.HotUpdateAssemblyName}");
            }

            if (AddressableAssetSettingsDefaultObject.Settings == null)
            {
                return Failure(
                    HotUpdatePublishErrorCode.AddressablesSettingsMissing,
                    "Addressables Settings 不存在");
            }

            if (!File.Exists(context.ManifestFilePath))
            {
                return Failure(
                    HotUpdatePublishErrorCode.ManifestAssetMissing,
                    $"找不到热更清单资源：{context.ManifestFilePath}");
            }

            if (!File.Exists(context.DllBytesFilePath))
            {
                return Failure(
                    HotUpdatePublishErrorCode.DllBytesAssetMissing,
                    $"找不到 DLL Bytes 资源：{context.DllBytesFilePath}");
            }

            return HotUpdatePublishValidationResult.Success();
        }

        private static HotUpdatePublishValidationResult Failure(
            HotUpdatePublishErrorCode errorCode,
            string message)
        {
            return HotUpdatePublishValidationResult.Failure(
                errorCode,
                message);
        }


    }
}

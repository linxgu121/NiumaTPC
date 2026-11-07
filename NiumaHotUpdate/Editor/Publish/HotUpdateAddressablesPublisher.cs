using System;
using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Settings;

namespace NiumaHotUpdate.Editor
{
    /// <summary>
    /// 基于基础客户端状态文件构建 Addressables 增量内容
    /// </summary>
    internal static class HotUpdateAddressablesPublisher
    {
        /// <summary>
        /// 校验发布环境并执行 Update a Previous Build
        /// </summary>
        public static HotUpdatePublishOperationResult Build(
            HotUpdatePublishContext context)
        {
            HotUpdatePublishValidationResult validationResult =
                HotUpdatePublishValidator.Validate(context);

            if (!validationResult.Succeeded)
            {
                return HotUpdatePublishOperationResult.Failure(
                    validationResult.ErrorCode,
                    validationResult.Message);
            }

            if (!File.Exists(context.ContentStatePath))
            {
                return HotUpdatePublishOperationResult.Failure(
                    HotUpdatePublishErrorCode.ContentStateMissing,
                    $"找不到基础客户端状态文件：{context.ContentStatePath}");
            }

            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;

            AddressablesPlayerBuildResult buildResult;

            try
            {
                AssetDatabase.SaveAssets();

                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

                buildResult =
                    ContentUpdateScript.BuildContentUpdate(
                        settings,
                        context.ContentStatePath);
            }
            catch (Exception exception)
            {
                return HotUpdatePublishOperationResult.Failure(
                    HotUpdatePublishErrorCode.AddressablesContentUpdateFailed,
                    $"Addressables 增量构建异常：{exception.Message}");
            }

            if (buildResult == null)
            {
                return HotUpdatePublishOperationResult.Failure(
                    HotUpdatePublishErrorCode.AddressablesBuildReturnedNull,
                    "Addressables 没有返回构建结果，请检查基础状态文件、Remote Catalog 和远程加载路径");
            }

            if (!string.IsNullOrWhiteSpace(buildResult.Error))
            {
                return HotUpdatePublishOperationResult.Failure(
                    HotUpdatePublishErrorCode.AddressablesContentUpdateFailed,
                    $"Addressables 增量构建失败：{buildResult.Error}");
            }

            return HotUpdatePublishOperationResult.Success(
                $"Addressables 增量构建完成，Duration={buildResult.Duration:F2}s，Locations={buildResult.LocationCount}，Output={buildResult.OutputPath}");
        }

        /// <summary>
        /// 完整构建 Addressables 并把生成的状态文件归档为当前基础客户端版本
        /// </summary>
        public static HotUpdatePublishOperationResult BuildAndArchiveBaseline(
            HotUpdatePublishContext context)
        {
            HotUpdatePublishValidationResult validationResult =
                HotUpdatePublishValidator.Validate(context);

            if (!validationResult.Succeeded)
            {
                return HotUpdatePublishOperationResult.Failure(
                    validationResult.ErrorCode,
                    validationResult.Message);
            }

            AddressableAssetSettings settings =
                AddressableAssetSettingsDefaultObject.Settings;

            AddressablesPlayerBuildResult buildResult;

            try
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

                AddressableAssetSettings.BuildPlayerContent(
                    out buildResult);
            }
            catch (Exception exception)
            {
                return HotUpdatePublishOperationResult.Failure(
                    HotUpdatePublishErrorCode.AddressablesFullBuildFailed,
                    $"Addressables 完整构建异常：{exception.Message}");
            }

            if (buildResult == null)
            {
                return HotUpdatePublishOperationResult.Failure(
                    HotUpdatePublishErrorCode.AddressablesBuildReturnedNull,
                    "Addressables 完整构建没有返回有效结果");
            }

            if (!string.IsNullOrWhiteSpace(buildResult.Error))
            {
                return HotUpdatePublishOperationResult.Failure(
                    HotUpdatePublishErrorCode.AddressablesFullBuildFailed,
                    $"Addressables 完整构建失败：{buildResult.Error}");
            }

            string generatedContentStatePath = Path.GetFullPath(
                ContentUpdateScript.GetContentStateDataPath(false, settings));

            if (!File.Exists(generatedContentStatePath))
            {
                return HotUpdatePublishOperationResult.Failure(
                    HotUpdatePublishErrorCode.GeneratedContentStateMissing,
                    $"完整构建没有生成状态文件：{generatedContentStatePath}");
            }

            try
            {
                string archiveDirectory =
                    Path.GetDirectoryName(context.ContentStatePath);

                Directory.CreateDirectory(archiveDirectory);

                if (!string.Equals(
                        generatedContentStatePath,
                        context.ContentStatePath,
                        StringComparison.OrdinalIgnoreCase))
                {
                    File.Copy(
                        generatedContentStatePath,
                        context.ContentStatePath,
                        true);
                }
            }
            catch (Exception exception)
            {
                return HotUpdatePublishOperationResult.Failure(
                    HotUpdatePublishErrorCode.ContentStateArchiveFailed,
                    $"基础版本状态文件归档失败：{exception.Message}");
            }

            return HotUpdatePublishOperationResult.Success(
                $"Addressables 完整构建与基础状态归档完成，Base={context.BasePlayerVersion}，State={context.ContentStatePath}");
        }

    }
}

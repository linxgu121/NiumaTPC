using System;
using System.IO;
using HybridCLR.Commands;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Settings;

namespace NiumaHotUpdate.Editor
{
    /// <summary>
    /// 编译 HybridCLR 热更程序集并替换 Addressable DLL Bytes 资源
    /// </summary>
    internal static class HotUpdateAssemblyPublisher
    {
        /// <summary>
        /// 重新执行前置校验，使用 Release 配置编译 DLL，并原子替换现有 Bytes 文件
        /// </summary>
        public static HotUpdatePublishOperationResult CompileAndReplace(
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

            try
            {
                CompileDllCommand.CompileDll(
                    context.Target,
                    false);
            }
            catch (Exception exception)
            {
                return HotUpdatePublishOperationResult.Failure(
                    HotUpdatePublishErrorCode.HotUpdateCompilationFailed,
                    $"热更程序集编译失败：{exception.Message}");
            }

            if (!File.Exists(context.CompiledDllPath))
            {
                return HotUpdatePublishOperationResult.Failure(
                    HotUpdatePublishErrorCode.CompiledDllMissing,
                    $"编译完成后找不到 DLL：{context.CompiledDllPath}");
            }

            long compiledDllLength =
                new FileInfo(context.CompiledDllPath).Length;

            if (compiledDllLength <= 0)
            {
                return HotUpdatePublishOperationResult.Failure(
                    HotUpdatePublishErrorCode.CompiledDllEmpty,
                    $"编译出的 DLL 内容为空：{context.CompiledDllPath}");
            }

            string temporaryFilePath =
                context.DllBytesFilePath + ".publish-temp";

            try
            {
                File.Copy(
                    context.CompiledDllPath,
                    temporaryFilePath,
                    true);

                long temporaryFileLength =
                    new FileInfo(temporaryFilePath).Length;

                if (temporaryFileLength != compiledDllLength)
                {
                    return HotUpdatePublishOperationResult.Failure(
                        HotUpdatePublishErrorCode.DllBytesReplaceFailed,
                        $"DLL 临时副本大小不一致，Source={compiledDllLength}，Temporary={temporaryFileLength}");
                }

                // 仅替换资源内容，保留原有 .meta 和 Addressables GUID
                File.Replace(
                    temporaryFilePath,
                    context.DllBytesFilePath,
                    null);

                AssetDatabase.ImportAsset(
                    HotUpdatePublishContext.DllBytesAssetPath,
                    ImportAssetOptions.ForceUpdate);
            }
            catch (Exception exception)
            {
                return HotUpdatePublishOperationResult.Failure(
                    HotUpdatePublishErrorCode.DllBytesReplaceFailed,
                    $"替换 DLL Bytes 失败：{exception.Message}");
            }
            finally
            {
                DeleteTemporaryFileSilently(temporaryFilePath);
            }

            return HotUpdatePublishOperationResult.Success(
                $"DLL 编译与替换完成，Bytes={compiledDllLength}，Path={context.DllBytesFilePath}");
        }

        /// <summary>
        /// 清理发布临时文件，避免清理异常覆盖真正的发布结果
        /// </summary>
        private static void DeleteTemporaryFileSilently(string temporaryFilePath)
        {
            try
            {
                if (File.Exists(temporaryFilePath))
                {
                    File.Delete(temporaryFilePath);
                }
            }
            catch
            {
                // 临时文件清理失败不覆盖原始发布结果
            }
        }
    }
}

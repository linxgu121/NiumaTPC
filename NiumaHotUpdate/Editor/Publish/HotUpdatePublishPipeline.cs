using System;

namespace NiumaHotUpdate.Editor
{
    /// <summary>
    /// 按固定顺序执行热更 DLL、清单和 Addressables 增量发布
    /// </summary>
    internal static class HotUpdatePublishPipeline
    {
        /// <summary>
        /// 执行完整热更发布，任一步失败时立即停止后续操作
        /// </summary>
        public static HotUpdatePublishOperationResult Publish(
            HotUpdatePublishContext context,
            Action<string, float> progress)
        {
            progress?.Invoke("正在执行发布前置校验",0.05f);

            HotUpdatePublishValidationResult validationResult = HotUpdatePublishValidator.Validate(context);

            if (!validationResult.Succeeded)
            {
                return HotUpdatePublishOperationResult.Failure(
                    validationResult.ErrorCode,
                    $"前置校验失败：{validationResult.Message}");
            }

            progress?.Invoke("正在编译并替换热更 DLL", 0.25f);

            HotUpdatePublishOperationResult assemblyResult =
                HotUpdateAssemblyPublisher.CompileAndReplace(context);

            if (!assemblyResult.Succeeded)
            {
                return Failure(
                    "DLL 发布",
                    assemblyResult);
            }

            progress?.Invoke(
                "正在计算 SHA256 并生成热更清单",
                0.55f);

            HotUpdatePublishOperationResult manifestResult =
                HotUpdateManifestPublisher.Write(context);

            if (!manifestResult.Succeeded)
            {
                return Failure(
                    "清单生成",
                    manifestResult);
            }

            progress?.Invoke(
                "正在构建 Addressables 增量内容",
                0.75f);

            HotUpdatePublishOperationResult addressablesResult =
                HotUpdateAddressablesPublisher.Build(context);

            if (!addressablesResult.Succeeded)
            {
                return Failure(
                    "Addressables 增量构建",
                    addressablesResult);
            }

            progress?.Invoke(
                "热更发布完成",
                1f);

            return HotUpdatePublishOperationResult.Success(
                $"热更发布完成，Base={context.BasePlayerVersion}，HotUpdate={context.HotUpdateVersion}");
        }

        private static HotUpdatePublishOperationResult Failure(
            string stage,
            HotUpdatePublishOperationResult result)
        {
            return HotUpdatePublishOperationResult.Failure(
                result.ErrorCode,
                $"{stage}失败：{result.Message}");
        }
    }
}

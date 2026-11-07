using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace NiumaHotUpdate.Editor
{
    /// <summary>
    /// 根据发布上下文生成热更清单并保留现有 AOT 元数据地址
    /// </summary>
    internal static class HotUpdateManifestPublisher
    {
        [Serializable]
        private sealed class AotMetadataManifestSnapshot
        {
            public string[] RequiredAotMetadataAssemblies;
        }

        /// <summary>
        /// 校验发布环境，计算最终 DLL 哈希并原子替换热更清单
        /// </summary>
        public static HotUpdatePublishOperationResult Write(
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

            AotMetadataManifestSnapshot existingManifest;

            try
            {
                string existingJson =
                    File.ReadAllText(context.ManifestFilePath);

                existingManifest =
                    JsonUtility.FromJson<AotMetadataManifestSnapshot>(
                        existingJson);
            }
            catch (Exception exception)
            {
                return HotUpdatePublishOperationResult.Failure(
                    HotUpdatePublishErrorCode.ManifestReadFailed,
                    $"读取现有热更清单失败：{exception.Message}");
            }

            if (existingManifest == null ||
                existingManifest.RequiredAotMetadataAssemblies == null)
            {
                return HotUpdatePublishOperationResult.Failure(
                    HotUpdatePublishErrorCode.ManifestInvalid,
                    "现有热更清单无效，无法安全保留 AOT 元数据地址");
            }

            string dllSha256;

            try
            {
                dllSha256 =
                    ComputeFileSha256(context.DllBytesFilePath);
            }
            catch (Exception exception)
            {
                return HotUpdatePublishOperationResult.Failure(
                    HotUpdatePublishErrorCode.DllHashCalculationFailed,
                    $"计算 DLL Bytes SHA256 失败：{exception.Message}");
            }

            var manifest = new HotUpdateManifest
            {
                BasePlayerVersion = context.BasePlayerVersion,
                HotUpdateVersion = context.HotUpdateVersion,
                Platform = context.Target.ToString(),

                RequiredHotUpdateAssemblies = new[]
                {
                    new HotUpdateAssemblyDescriptor
                    {
                        Address =
                            HotUpdatePublishContext.HotUpdateAssemblyAddress,

                        Sha256 = dllSha256
                    }
                },

                RequiredAotMetadataAssemblies =
                    existingManifest.RequiredAotMetadataAssemblies,

                EntryAssemblyName =
                    HotUpdatePublishContext.HotUpdateAssemblyName,

                EntryTypeName =
                    HotUpdatePublishContext.HotUpdateEntryTypeName
            };

            string temporaryFilePath =
                context.ManifestFilePath + ".publish-temp";

            try
            {
                string json =
                    JsonUtility.ToJson(manifest, true);

                File.WriteAllText(
                    temporaryFilePath,
                    json + Environment.NewLine,
                    new UTF8Encoding(false));

                // 仅替换 JSON 内容，保留原有 .meta 和 Addressables GUID
                File.Replace(
                    temporaryFilePath,
                    context.ManifestFilePath,
                    null);

                AssetDatabase.ImportAsset(
                    HotUpdatePublishContext.ManifestAssetPath,
                    ImportAssetOptions.ForceUpdate);
            }
            catch (Exception exception)
            {
                return HotUpdatePublishOperationResult.Failure(
                    HotUpdatePublishErrorCode.ManifestWriteFailed,
                    $"写入热更清单失败：{exception.Message}");
            }
            finally
            {
                DeleteTemporaryFileSilently(
                    temporaryFilePath);
            }

            return HotUpdatePublishOperationResult.Success(
                $"热更清单写入完成，Base={context.BasePlayerVersion}，HotUpdate={context.HotUpdateVersion}，SHA256={dllSha256}");
        }

        private static string ComputeFileSha256(string filePath)
        {
            using (FileStream stream =
                   File.OpenRead(filePath))
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] hash = sha256.ComputeHash(stream);
                var builder =
                    new StringBuilder(hash.Length * 2);

                foreach (byte value in hash)
                {
                    builder.Append(value.ToString("x2"));
                }

                return builder.ToString();
            }
        }

        /// <summary>
        /// 清理清单发布临时文件，避免清理异常覆盖原始发布结果
        /// </summary>
        private static void DeleteTemporaryFileSilently(
            string temporaryFilePath)
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

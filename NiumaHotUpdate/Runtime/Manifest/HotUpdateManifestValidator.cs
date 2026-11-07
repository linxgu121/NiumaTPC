using System;
using System.Collections.Generic;

namespace NiumaHotUpdate
{
    /// <summary>
    /// 校验远程发布清单能否安全用于当前基础客户端
    /// </summary>
    public static class HotUpdateManifestValidator
    {
        /// <summary>
        /// 校验清单必填字段、客户端版本、运行平台和程序集地址唯一性
        /// </summary>
        public static HotUpdateManifestValidationResult Validate(HotUpdateManifest manifest, string currentBasePlayerVersion, string currentPlatform)
        {
            if (string.IsNullOrWhiteSpace(currentBasePlayerVersion))
            {
                throw new ArgumentException("当前基础客户端版本不能为空", nameof(currentBasePlayerVersion));
            }

            if (string.IsNullOrWhiteSpace(currentPlatform))
            {
                throw new ArgumentException("当前运行平台不能为空", nameof(currentPlatform));
            }

            if (manifest == null)
            {
                return HotUpdateManifestValidationResult.Failure(
                    HotUpdateManifestValidationErrorCode.ManifestMissing,
                    "没有获得有效的热更发布清单");
            }

            if (string.IsNullOrWhiteSpace(manifest.BasePlayerVersion))
            {
                return HotUpdateManifestValidationResult.Failure(
                    HotUpdateManifestValidationErrorCode.BasePlayerVersionMissing,
                    "热更发布清单缺少基础客户端版本");
            }

            if (string.IsNullOrWhiteSpace(manifest.HotUpdateVersion))
            {
                return HotUpdateManifestValidationResult.Failure(
                    HotUpdateManifestValidationErrorCode.HotUpdateVersionMissing,
                    "热更发布清单缺少热更版本");
            }

            if (string.IsNullOrWhiteSpace(manifest.Platform))
            {
                return HotUpdateManifestValidationResult.Failure(
                    HotUpdateManifestValidationErrorCode.PlatformMissing,
                    "热更发布清单缺少目标平台");
            }

            if (!string.Equals(manifest.BasePlayerVersion, currentBasePlayerVersion, StringComparison.Ordinal))
            {
                return HotUpdateManifestValidationResult.Failure(
                    HotUpdateManifestValidationErrorCode.BasePlayerVersionMismatch,
                    $"清单基础客户端版本不兼容，Expected={currentBasePlayerVersion}, Actual={manifest.BasePlayerVersion}");
            }

            if (!string.Equals(manifest.Platform, currentPlatform, StringComparison.Ordinal))
            {
                return HotUpdateManifestValidationResult.Failure(
                    HotUpdateManifestValidationErrorCode.PlatformMismatch,
                    $"清单运行平台不兼容，Expected={currentPlatform}, Actual={manifest.Platform}");
            }

            if (manifest.RequiredHotUpdateAssemblies == null || manifest.RequiredHotUpdateAssemblies.Length == 0)
            {
                return HotUpdateManifestValidationResult.Failure(
                    HotUpdateManifestValidationErrorCode.HotUpdateAssemblyListMissing,
                    "热更发布清单没有提供热更程序集地址");
            }

            if (manifest.RequiredAotMetadataAssemblies == null)
            {
                return HotUpdateManifestValidationResult.Failure(
                    HotUpdateManifestValidationErrorCode.AotMetadataAssemblyListMissing,
                    "热更发布清单没有提供 AOT 补充元数据地址列表");
            }

            var uniqueAddresses = new HashSet<string>(StringComparer.Ordinal);

            foreach (HotUpdateAssemblyDescriptor assembly in manifest.RequiredHotUpdateAssemblies)
            {
                if (assembly == null)
                {
                    return HotUpdateManifestValidationResult.Failure(
                        HotUpdateManifestValidationErrorCode.HotUpdateAssemblyDescriptorMissing,
                        "热更程序集列表包含空描述");
                }

                if (string.IsNullOrWhiteSpace(assembly.Address))
                {
                    return HotUpdateManifestValidationResult.Failure(
                        HotUpdateManifestValidationErrorCode.HotUpdateAssemblyAddressInvalid,
                        "热更程序集列表包含空地址");
                }

                if (!IsValidSha256(assembly.Sha256))
                {
                    return HotUpdateManifestValidationResult.Failure(
                        HotUpdateManifestValidationErrorCode.HotUpdateAssemblyHashInvalid,
                        $"热更程序集 SHA256 无效，Address={assembly.Address}");
                }

                if (!uniqueAddresses.Add(assembly.Address))
                {
                    return HotUpdateManifestValidationResult.Failure(
                        HotUpdateManifestValidationErrorCode.DuplicateAssemblyAddress,
                        $"清单包含重复的程序集地址：{assembly.Address}");
                }
            }

            foreach (string address in manifest.RequiredAotMetadataAssemblies)
            {
                if (string.IsNullOrWhiteSpace(address))
                {
                    return HotUpdateManifestValidationResult.Failure(
                        HotUpdateManifestValidationErrorCode.AotMetadataAssemblyAddressInvalid,
                        "AOT 补充元数据列表包含空地址");
                }

                if (!uniqueAddresses.Add(address))
                {
                    return HotUpdateManifestValidationResult.Failure(
                        HotUpdateManifestValidationErrorCode.DuplicateAssemblyAddress,
                        $"清单包含重复的程序集地址：{address}");
                }
            }

            if (string.IsNullOrWhiteSpace(manifest.EntryAssemblyName))
            {
                return HotUpdateManifestValidationResult.Failure(
                    HotUpdateManifestValidationErrorCode.EntryAssemblyNameMissing,
                    "热更发布清单缺少入口程序集名称");
            }

            if (string.IsNullOrWhiteSpace(manifest.EntryTypeName))
            {
                return HotUpdateManifestValidationResult.Failure(
                    HotUpdateManifestValidationErrorCode.EntryTypeNameMissing,
                    "热更发布清单缺少入口类型名称");
            }

            return HotUpdateManifestValidationResult.Success();
        }

        /// <summary>
        /// 校验 SHA256 是否为完整的 64 位十六进制字符串
        /// </summary>
        private static bool IsValidSha256(string sha256)
        {
            if (string.IsNullOrWhiteSpace(sha256) ||
                sha256.Length != 64)
            {
                return false;
            }

            foreach (char value in sha256)
            {
                bool isNumber = value >= '0' && value <= '9';
                bool isLowerHex = value >= 'a' && value <= 'f';
                bool isUpperHex = value >= 'A' && value <= 'F';

                if (!isNumber && !isLowerHex && !isUpperHex)
                {
                    return false;
                }
            }

            return true;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using HybridCLR;
using NiumaCore.Module;
using NiumaResource;
using UnityEngine;

namespace NiumaHotUpdate
{
    /// <summary>
    /// 按发布清单加载 HybridCLR 元数据、热更程序集并启动热更入口
    /// </summary>
    public sealed class HotUpdateService
    {
        #region Dependencies(依赖)

        private readonly IResourceService _resourceService;
        private readonly HotUpdateManifestLoader _manifestLoader;

        #endregion

        #region Runtime State(运行时状态)

        private bool _isInitializing;
        private HotUpdateManifest _manifest;
        private IHotUpdateEntry _entry;

        #endregion

        #region Properties(状态属性)

        public bool IsInitialized => _entry != null;

        public string CurrentVersion => _manifest?.HotUpdateVersion ?? string.Empty;

        public IHotUpdateEntry Entry => _entry;

        #endregion

        #region Constructor(构造方法)

        public HotUpdateService(IResourceService resourceService)
        {
            _resourceService = resourceService ?? throw new ArgumentNullException(nameof(resourceService));
            _manifestLoader = new HotUpdateManifestLoader(resourceService);
        }

        #endregion

        #region Public API(公共接口)

        /// <summary>
        /// 初始化当前平台的热更运行环境并执行清单指定的入口
        /// 重复调用会返回首次初始化成功的结果
        /// </summary>
        public async Task<HotUpdateInitializationResult> InitializeAsync(GameContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (IsInitialized)
            {
                return HotUpdateInitializationResult.Success(_manifest, _entry);
            }

            if (_isInitializing)
            {
                return HotUpdateInitializationResult.Failure(
                    HotUpdateInitializationErrorCode.InitializationInProgress,
                    "热更运行环境正在初始化");
            }

            if (!HotUpdateRuntimeEnvironment.TryGetCurrentPlatformName(out string platformName))
            {
                return HotUpdateInitializationResult.Failure(
                    HotUpdateInitializationErrorCode.UnsupportedPlatform,
                    $"当前运行平台不受支持，RuntimePlatform={Application.platform}");
            }

            _isInitializing = true;

            try
            {
                HotUpdateManifestLoadResult manifestResult = await _manifestLoader.LoadAsync(
                    HotUpdateRuntimeEnvironment.CurrentBasePlayerVersion,
                    platformName);

                if (!manifestResult.Succeeded)
                {
                    return HotUpdateInitializationResult.Failure(
                        HotUpdateInitializationErrorCode.ManifestLoadFailed,
                        $"热更发布清单加载失败，Error={manifestResult.ErrorCode}, " +
                        $"ResourceError={manifestResult.ResourceErrorCode}, " +
                        $"ValidationError={manifestResult.ValidationErrorCode}, " +
                        $"Detail={manifestResult.ErrorMessage}");
                }

                HotUpdateManifest manifest = manifestResult.Manifest;

                foreach (string address in manifest.RequiredAotMetadataAssemblies)
                {
                    ResourceLoadResult<TextAsset> metadataResult = await _resourceService.LoadAsync<TextAsset>(address);

                    if (!metadataResult.Succeeded)
                    {
                        return HotUpdateInitializationResult.Failure(
                            HotUpdateInitializationErrorCode.AotMetadataResourceLoadFailed,
                            $"AOT 补充元数据资源加载失败，Address={address}, " +
                            $"ResourceError={metadataResult.ErrorCode}, Detail={metadataResult.ErrorMessage}");
                    }

                    using (IResourceLease<TextAsset> lease = metadataResult.Lease)
                    {
                        LoadImageErrorCode loadCode = RuntimeApi.LoadMetadataForAOTAssembly(
                            lease.Asset.bytes,
                            HomologousImageMode.SuperSet);

                        if (loadCode != LoadImageErrorCode.OK)
                        {
                            return HotUpdateInitializationResult.Failure(
                                HotUpdateInitializationErrorCode.AotMetadataRegistrationFailed,
                                $"AOT 补充元数据注册失败，Address={address}, HybridCLRError={loadCode}");
                        }
                    }
                }

                var loadedAssemblies = new Dictionary<string, Assembly>(StringComparer.Ordinal);

                foreach (HotUpdateAssemblyDescriptor descriptor in manifest.RequiredHotUpdateAssemblies)
                {
                    string address = descriptor.Address;

                    ResourceLoadResult<TextAsset> assemblyResult =
                        await _resourceService.LoadAsync<TextAsset>(
                            address);

                    if (!assemblyResult.Succeeded)
                    {
                        return HotUpdateInitializationResult.Failure(
                            HotUpdateInitializationErrorCode.HotUpdateAssemblyResourceLoadFailed,
                            $"热更程序集资源加载失败，Address={address}, " +
                            $"ResourceError={assemblyResult.ErrorCode}, Detail={assemblyResult.ErrorMessage}");
                    }

                    Assembly assembly;

                    using (IResourceLease<TextAsset> lease = assemblyResult.Lease)
                    {
                        byte[] assemblyBytes = lease.Asset.bytes;
                        string actualSha256;

                        try
                        {
                            actualSha256 = ComputeSha256(assemblyBytes);
                        }
                        catch (Exception exception)
                        {
                            return HotUpdateInitializationResult.Failure(
                                HotUpdateInitializationErrorCode.HotUpdateAssemblyHashCalculationFailed,
                                $"计算热更程序集 SHA256 失败，Address={address}, Error={exception.Message}");
                        }

                        if (!string.Equals(
                                actualSha256,
                                descriptor.Sha256,
                                StringComparison.OrdinalIgnoreCase))
                        {
                            return HotUpdateInitializationResult.Failure(
                                HotUpdateInitializationErrorCode.HotUpdateAssemblyHashMismatch,
                                $"热更程序集 SHA256 不一致，Address={address}, Expected={descriptor.Sha256}, Actual={actualSha256}");
                        }

                        try
                        {
                            assembly =
                                Assembly.Load(assemblyBytes);
                        }
                        catch (Exception exception)
                        {
                            return HotUpdateInitializationResult.Failure(
                                HotUpdateInitializationErrorCode.HotUpdateAssemblyLoadFailed,
                                $"热更程序集加载失败，Address={address}, Error={exception.Message}");
                        }
                    }

                    string assemblyName = assembly.GetName().Name;

                    if (!loadedAssemblies.TryAdd(
                            assemblyName,
                            assembly))
                    {
                        return HotUpdateInitializationResult.Failure(
                            HotUpdateInitializationErrorCode.DuplicateHotUpdateAssemblyName,
                            $"热更清单加载了重复的程序集名称，Assembly={assemblyName}");
                    }
                }

                if (!loadedAssemblies.TryGetValue(manifest.EntryAssemblyName, out Assembly entryAssembly))
                {
                    return HotUpdateInitializationResult.Failure(
                        HotUpdateInitializationErrorCode.EntryAssemblyNotFound,
                        $"找不到热更入口程序集，Assembly={manifest.EntryAssemblyName}");
                }

                Type entryType = entryAssembly.GetType(manifest.EntryTypeName, false);

                if (entryType == null)
                {
                    return HotUpdateInitializationResult.Failure(
                        HotUpdateInitializationErrorCode.EntryTypeNotFound,
                        $"找不到热更入口类型，Type={manifest.EntryTypeName}");
                }

                if (!typeof(IHotUpdateEntry).IsAssignableFrom(entryType))
                {
                    return HotUpdateInitializationResult.Failure(
                        HotUpdateInitializationErrorCode.EntryContractMismatch,
                        $"热更入口类型没有实现 IHotUpdateEntry，Type={manifest.EntryTypeName}");
                }

                IHotUpdateEntry entry;

                try
                {
                    entry = (IHotUpdateEntry)Activator.CreateInstance(entryType);
                }
                catch (Exception exception)
                {
                    return HotUpdateInitializationResult.Failure(
                        HotUpdateInitializationErrorCode.EntryCreationFailed,
                        $"热更入口实例创建失败，Type={manifest.EntryTypeName}, Error={exception.Message}");
                }

                try
                {
                    entry.Initialize(new HotUpdateEntryContext(context, manifest.HotUpdateVersion));
                }
                catch (Exception exception)
                {
                    return HotUpdateInitializationResult.Failure(
                        HotUpdateInitializationErrorCode.EntryInitializationFailed,
                        $"热更入口初始化失败，Type={manifest.EntryTypeName}, Error={exception}");
                }

                _manifest = manifest;
                _entry = entry;

                return HotUpdateInitializationResult.Success(_manifest, _entry);
            }
            finally
            {
                _isInitializing = false;
            }
        }

        #endregion

                #region Hashing(哈希计算)

        private static string ComputeSha256(byte[] bytes)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] hash = sha256.ComputeHash(bytes);
                var builder = new StringBuilder(hash.Length * 2);

                foreach (byte value in hash)
                {
                    builder.Append(value.ToString("x2"));
                }

                return builder.ToString();
            }
        }

        #endregion
    }
}

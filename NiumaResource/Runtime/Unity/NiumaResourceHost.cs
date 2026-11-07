using System;
using System.Threading.Tasks;
using NiumaCore.Module;
using UnityEngine;

namespace NiumaResource
{
    /// <summary>
    /// NiumaResource 的 Unity 生命周期宿主
    /// 负责创建资源服务、注册到 GameContext 并在销毁时完成清理
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NiumaResourceHost : MonoBehaviour
    {
        #region Inspector

        [SerializeField]
        [Tooltip("开启后输出资源服务启动和停止日志")]
        private bool _verboseLogging = true;

        [SerializeField]
        [Tooltip("开启后在正式客户端启动时清理当前 Catalog 已不再引用的旧 Bundle，编辑器和批处理模式不会执行")]
        private bool _cleanUnusedBundleCacheOnStart = true;

        #endregion

        #region Runtime State

        private GameContext _context;
        private static NiumaResourceHost _activeHost;
        private AddressableResourceService _resourceService;

        private int _lifecycleVersion;

        public bool IsStarting { get; private set; }

        /// <summary>
        /// 资源服务是否已经初始化并注册到当前 GameContext
        /// </summary>
        public bool IsStarted =>
            _context != null &&
            _resourceService != null &&
            _resourceService.IsInitialized &&
            ReferenceEquals(_context.GetService<IResourceService>(), _resourceService);

        /// <summary>
        /// 最近一次启动失败原因
        /// 没有错误时为空字符串
        /// </summary>
        public string LastError { get; private set; } = string.Empty;

        #endregion

        #region Unity Lifecycle

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetActiveHost()
        {
            // 兼容编辑器关闭 Domain Reload 的运行方式
            _activeHost = null;
        }

        private void OnDestroy()
        {
            Stop();
        }

        #endregion

        #region Public API

        /// <summary>
        /// 检查当前进程是否存在已经完成初始化的活动资源服务
        /// 供无法直接持有 GameContext 的场景运行时组件使用
        /// </summary>
        public static bool TryGetActiveService(out IResourceService resourceService)
        {
            NiumaResourceHost activeHost = _activeHost;

            if (activeHost != null &&
                activeHost.IsStarted &&
                activeHost._resourceService != null &&
                activeHost._resourceService.IsInitialized)
            {
                resourceService = activeHost._resourceService;
                return true;
            }

            resourceService = null;
            return false;
        }

        /// <summary>
        /// 检查 GameContext 与现有服务注册后异步创建并启动资源服务
        /// 同一个 GameContext 重复启动保持幂等
        /// </summary>
        public async Task<bool> TryStartAsync(GameContext context)
        {
            if (IsStarted)
            {
                if (ReferenceEquals(_context, context))
                {
                    _activeHost = this;
                    LastError = string.Empty;
                    return true;
                }

                return FailStart("NiumaResourceHost 已经绑定其他 GameContext");
            }

            if (IsStarting)
            {
                return FailStart("NiumaResourceHost 正在启动，不能重复提交启动请求");
            }

            if (_activeHost != null && _activeHost != this)
            {
                if (_activeHost.IsStarted || _activeHost.IsStarting)
                {
                    return FailStart("当前进程已经存在其他活动的 NiumaResourceHost");
                }

                _activeHost = null;
            }

            // 清理可能存在的未完成启动状态
            Stop();

            if (context == null)
            {
                return FailStart("启动资源服务时 GameContext 不能为空");
            }

            if (context.TryGetService(out IResourceService existingService))
            {
                return FailStart(
                    $"GameContext 已经注册其他 IResourceService: {existingService.GetType().FullName}");
            }

            int lifecycleVersion = _lifecycleVersion;
            AddressableResourceService resourceService = null;
            IsStarting = true;

            try
            {
                resourceService = new AddressableResourceService();
                await resourceService.InitializeAsync();

                // 等待期间 Stop 或销毁宿主后，不允许旧任务重新注册服务
                if (this == null || lifecycleVersion != _lifecycleVersion)
                {
                    resourceService.Shutdown();
                    return false;
                }

                if (_cleanUnusedBundleCacheOnStart && !Application.isEditor && !Application.isBatchMode)
                {
                    long cacheBytesBefore = Caching.ready
                        ? Caching.currentCacheForWriting.spaceOccupied
                        : -1L;

                    ResourceCacheCleanupResult cleanupResult =
                        await resourceService.CleanUnusedBundleCacheAsync();

                    // 缓存清理等待期间宿主可能已经停止或销毁
                    if (this == null || lifecycleVersion != _lifecycleVersion)
                    {
                        resourceService.Shutdown();
                        return false;
                    }

                    if (!cleanupResult.Succeeded)
                    {
                        Debug.LogWarning(
                            $"[NiumaResource] 清理废弃 Bundle 缓存失败: " +
                            $"ErrorCode={cleanupResult.ErrorCode}, " +
                            $"Error={cleanupResult.ErrorMessage}",
                            this);
                    }
                    else if (_verboseLogging)
                    {
                        long cacheBytesAfter = Caching.ready
                            ? Caching.currentCacheForWriting.spaceOccupied
                            : -1L;

                        string cacheSizeMessage =
                            cacheBytesBefore >= 0L && cacheBytesAfter >= 0L
                                ? $"Before={cacheBytesBefore / 1048576d:F2} MiB, " +
                                  $"After={cacheBytesAfter / 1048576d:F2} MiB"
                                : "缓存容量统计不可用";

                        Debug.Log(
                            $"[NiumaResource] 已清理当前 Catalog 不再引用的旧 Bundle: " +
                            cacheSizeMessage,
                            this);
                    }
                }

                context.RegisterService<IResourceService>(resourceService);

                _context = context;
                _resourceService = resourceService;
                _activeHost = this;

                LastError = string.Empty;

                if (_verboseLogging)
                {
                    Debug.Log("[NiumaResource] Addressables 初始化完成，资源服务已经注册到 GameContext", this);
                }

                return true;
            }
            catch (Exception exception)
            {
                resourceService?.Shutdown();

                if (this == null || lifecycleVersion != _lifecycleVersion)
                {
                    return false;
                }

                return FailStart($"创建资源服务时发生异常: {exception.Message}");
            }
            finally
            {
                if (this != null && lifecycleVersion == _lifecycleVersion)
                {
                    IsStarting = false;
                }
            }
        }

        /// <summary>
        /// 从 GameContext 注销当前资源服务并释放全部底层资源
        /// 重复调用保持安全
        /// </summary>
        public void Stop()
        {
            _lifecycleVersion++;
            IsStarting = false;

            GameContext context = _context;

            AddressableResourceService resourceService = _resourceService;

            bool hadService = context != null || resourceService != null;

            if (_activeHost == this)
            {
                _activeHost = null;
            }

            // 先清空宿主状态，避免清理期间再次访问旧服务
            _context = null;
            _resourceService = null;

            if (context != null &&
                context.TryGetService(
                    out IResourceService registeredService) &&
                ReferenceEquals(
                    registeredService,
                    resourceService))
            {
                context.UnregisterService<IResourceService>();
            }

            resourceService?.Shutdown();

            if (hadService && _verboseLogging)
            {
                Debug.Log("[NiumaResource] 资源服务已经停止", this);
            }
        }

        #endregion

        #region Helpers

        // 记录资源服务启动失败原因，供 Bootstrap 输出完整错误
        private bool FailStart(string message)
        {
            LastError = string.IsNullOrWhiteSpace(message) ? "资源服务启动失败" : message.Trim();
            return false;
        }

        #endregion
    }
}

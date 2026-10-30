using System;
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

        #endregion

        #region Runtime State

        private GameContext _context;
        private static NiumaResourceHost _activeHost;
        private AddressableResourceService _resourceService;

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
        /// 检查 GameContext 与现有服务注册后创建并启动资源服务
        /// 同一个 GameContext 重复启动保持幂等
        /// </summary>
        public bool TryStart(GameContext context, out string error)
        {
            if (IsStarted)
            {
                if (ReferenceEquals(_context, context))
                {
                    _activeHost = this;
                    LastError = string.Empty;
                    error = string.Empty;
                    return true;
                }

                return FailStart("NiumaResourceHost 已经绑定其他 GameContext", out error);
            }

            if (_activeHost != null && _activeHost != this)
            {
                if (_activeHost.IsStarted)
                {
                    return FailStart("当前进程已经存在其他活动的 NiumaResourceHost", out error);
                }

                _activeHost = null;
            }

            // 清理可能存在的未完成启动状态
            Stop();

            if (context == null)
            {
                return FailStart("启动资源服务时 GameContext 不能为空", out error);
            }

            if (context.TryGetService(out IResourceService existingService))
            {
                return FailStart(
                    $"GameContext 已经注册其他 IResourceService: " +
                    $"{existingService.GetType().FullName}",
                    out error);
            }

            AddressableResourceService resourceService = null;

            try
            {
                resourceService = new AddressableResourceService();

                resourceService.Initialize();

                context.RegisterService<IResourceService>(resourceService);

                _context = context;
                _resourceService = resourceService;
                _activeHost = this;

                LastError = string.Empty;
                error = string.Empty;

                if (_verboseLogging)
                {
                    Debug.Log("[NiumaResource] 资源服务启动并注册到 GameContext", this);
                }

                return true;
            }
            catch (Exception exception)
            {
                resourceService?.Shutdown();

                return FailStart($"创建资源服务时发生异常: {exception.Message}", out error);
            }
        }

        /// <summary>
        /// 从 GameContext 注销当前资源服务并释放全部底层资源
        /// 重复调用保持安全
        /// </summary>
        public void Stop()
        {
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

        private bool FailStart(string message, out string error)
        {
            LastError = string.IsNullOrWhiteSpace(message) ? "资源服务启动失败" : message.Trim();

            error = LastError;
            return false;
        }

        #endregion
    }
}
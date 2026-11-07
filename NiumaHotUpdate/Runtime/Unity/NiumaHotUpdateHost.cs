using System;
using System.Threading.Tasks;
using NiumaCore.Module;
using NiumaResource;
using UnityEngine;

namespace NiumaHotUpdate
{
    /// <summary>
    /// NiumaHotUpdate 的 Unity 生命周期宿主
    /// 负责从 GameContext 取得资源服务并启动热更运行环境
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NiumaHotUpdateHost : MonoBehaviour
    {
        #region Inspector(面板配置)

        [SerializeField]
        [Tooltip("开启后输出热更运行环境的启动和停止日志")]
        private bool _verboseLogging = true;

        #endregion

        #region Runtime State(运行时状态)

        private GameContext _context;
        private HotUpdateService _service;
        private int _lifecycleVersion;

        public bool IsStarting { get; private set; }

        /// <summary>
        /// 热更运行环境是否已经完成初始化
        /// </summary>
        public bool IsStarted =>
            _context != null &&
            _service != null &&
            _service.IsInitialized;

        /// <summary>
        /// 当前成功加载的热更版本
        /// 尚未启动时为空字符串
        /// </summary>
        public string CurrentVersion => _service?.CurrentVersion ?? string.Empty;

        /// <summary>
        /// 最近一次启动失败原因
        /// 没有错误时为空字符串
        /// </summary>
        public string LastError { get; private set; } = string.Empty;

        #endregion

        #region Unity Lifecycle(Unity 生命周期)

        private void OnDestroy()
        {
            Stop();
        }

        #endregion

        #region Public API(公共接口)

        /// <summary>
        /// 检查 GameContext 中的资源服务并异步初始化热更运行环境
        /// 同一个 GameContext 重复启动保持幂等
        /// </summary>
        public async Task<bool> TryStartAsync(GameContext context)
        {
            if (IsStarted)
            {
                if (ReferenceEquals(_context, context))
                {
                    LastError = string.Empty;
                    return true;
                }

                return FailStart("NiumaHotUpdateHost 已经绑定其他 GameContext");
            }

            if (IsStarting)
            {
                return FailStart("NiumaHotUpdateHost 正在启动，不能重复提交启动请求");
            }

            Stop();

            if (context == null)
            {
                return FailStart("启动热更运行环境时 GameContext 不能为空");
            }

            if (!context.TryGetService(out IResourceService resourceService))
            {
                return FailStart("GameContext 没有注册 IResourceService，必须先启动 NiumaResourceHost");
            }

            int lifecycleVersion = _lifecycleVersion;
            var service = new HotUpdateService(resourceService);
            IsStarting = true;

            try
            {
                HotUpdateInitializationResult initializationResult =
                    await service.InitializeAsync(context);

                // 异步等待期间宿主可能已经销毁或被停止
                if (this == null || lifecycleVersion != _lifecycleVersion)
                {
                    return false;
                }

                if (!initializationResult.Succeeded)
                {
                    return FailStart(
                        $"热更运行环境初始化失败，" +
                        $"ErrorCode={initializationResult.ErrorCode}, " +
                        $"Detail={initializationResult.ErrorMessage}");
                }

                _context = context;
                _service = service;
                LastError = string.Empty;

                if (_verboseLogging)
                {
                    Debug.Log(
                        $"[NiumaHotUpdate] 热更运行环境初始化完成，" +
                        $"Version={initializationResult.HotUpdateVersion}",
                        this);
                }

                return true;
            }
            catch (Exception exception)
            {
                if (this == null || lifecycleVersion != _lifecycleVersion)
                {
                    return false;
                }

                return FailStart($"启动热更运行环境时发生异常：{exception}");
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
        /// 清除宿主持有的上下文与服务引用
        /// 已加载的托管程序集无法在当前进程中卸载
        /// </summary>
        public void Stop()
        {
            _lifecycleVersion++;
            IsStarting = false;

            bool hadService = _context != null || _service != null;

            _context = null;
            _service = null;

            if (hadService && _verboseLogging)
            {
                Debug.Log("[NiumaHotUpdate] 热更宿主已经停止，已加载程序集将在进程结束时释放",this);
            }
        }

        #endregion

        #region Helpers(辅助方法)

        // 记录热更宿主启动失败原因，供 Bootstrap 输出完整错误
        private bool FailStart(string message)
        {
            LastError = string.IsNullOrWhiteSpace(message)
                ? "热更运行环境启动失败"
                : message.Trim();

            return false;
        }

        #endregion
    }
}

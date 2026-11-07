using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NiumaResource;
using UnityEngine;

namespace NiumaTPC.Role
{
    /// <summary>
    /// 异步加载并管理角色展示实例
    /// 展示实例存活期间持续持有对应资源 Lease
    /// </summary>
    [DisallowMultipleComponent]
    public class CharacterPreviewPresenter : MonoBehaviour
    {
        #region Inspector

        [SerializeField]
        [Tooltip("展示模型的实例化挂点")]
        private Transform _previewRoot;

        [SerializeField]
        [Tooltip("开启后输出角色资源加载、实例化和动画启动耗时")]
        private bool _logLoadTiming;

        #endregion

        #region Runtime State

        // 缓存本次大厅会话中已经查看过的角色资源
        // 这里只保留资源 Lease，不缓存角色实例
        private readonly Dictionary<string, IResourceLease<GameObject>> _previewAssetCache =
            new Dictionary<string, IResourceLease<GameObject>>(StringComparer.Ordinal);

        private GameObject _previewInstance;
        private ICharacterPreview _previewAnimation;

        private string _sourceAddress = string.Empty;
        private int _requestVersion;

        public bool IsLoading { get; private set; }

        public string LastError { get; private set; } = string.Empty;

        #endregion

        #region Unity Lifecycle

        private void OnDisable()
        {
            Clear();
        }

        #endregion

        #region Public API

        /// <summary>
        /// 检查展示环境与资源地址后异步加载候选角色
        /// 已查看角色的资源 Lease 会保留到展示器清理，快速切换时只有最后一次请求可以替换当前预览
        /// </summary>
        public async Task<bool> TryShowAsync(string address, IResourceService resourceService)
        {
            int requestVersion = ++_requestVersion;
            IsLoading = false;
            LastError = string.Empty;

            if (!isActiveAndEnabled)
                return FailShow("角色展示组件未启用");

            if (_previewRoot == null)
                return FailShow("没有绑定角色展示挂点");

            if (resourceService == null || !resourceService.IsInitialized)
                return FailShow("资源服务尚未初始化");

            if (string.IsNullOrWhiteSpace(address))
                return FailShow("角色预览资源地址为空");

            string normalizedAddress = address.Trim();
            double totalStartedAt = Time.realtimeSinceStartupAsDouble;

            if (_previewInstance != null &&
                _previewAssetCache.TryGetValue(normalizedAddress, out IResourceLease<GameObject> currentLease) &&
                currentLease != null &&
                !currentLease.IsReleased &&
                string.Equals(_sourceAddress, normalizedAddress, StringComparison.Ordinal))
            {
                return true;
            }

            IsLoading = true;

            IResourceLease<GameObject> candidateLease = null;
            GameObject candidateInstance = null;
            ICharacterPreview candidateAnimation = null;
            bool candidateLeaseOwnedByCache = false;

            try
            {
                double loadStartedAt = Time.realtimeSinceStartupAsDouble;

                if (_previewAssetCache.TryGetValue(normalizedAddress, out candidateLease))
                {
                    if (candidateLease == null || candidateLease.IsReleased)
                    {
                        _previewAssetCache.Remove(normalizedAddress);
                        candidateLease = null;
                    }
                    else
                    {
                        candidateLeaseOwnedByCache = true;
                    }
                }

                if (candidateLease == null)
                {
                    ResourceLoadResult<GameObject> loadResult =
                        await resourceService.LoadAsync<GameObject>(normalizedAddress);

                    // 页面关闭或用户切换到其他角色后，旧请求不能覆盖最新结果
                    if (requestVersion != _requestVersion || !isActiveAndEnabled)
                    {
                        loadResult.Lease?.Dispose();
                        return false;
                    }

                    if (!loadResult.Succeeded || loadResult.Lease == null)
                    {
                        return FailShow(
                            $"角色预览资源加载失败 Code={loadResult.ErrorCode}, " +
                            $"Message={loadResult.ErrorMessage}");
                    }

                    candidateLease = loadResult.Lease;
                }

                double loadCompletedAt = Time.realtimeSinceStartupAsDouble;

                // 缓存命中路径没有异步等待，也仍需防止组件在调用前已经失效
                if (requestVersion != _requestVersion || !isActiveAndEnabled)
                    return false;

                GameObject prefab = candidateLease.Asset;
                double instantiateStartedAt = Time.realtimeSinceStartupAsDouble;

                candidateInstance = Instantiate(prefab, _previewRoot, false);
                candidateInstance.transform.localPosition = Vector3.zero;
                candidateInstance.transform.localRotation = Quaternion.identity;

                double instantiateCompletedAt = Time.realtimeSinceStartupAsDouble;

                // 展示动画组件约定位于预制体根节点
                candidateAnimation = candidateInstance.GetComponent<ICharacterPreview>();

                if (candidateAnimation == null)
                    return FailShow($"展示资源 {normalizedAddress} 的根节点缺少 CharacterAnimationPlay");

                if (!candidateAnimation.TryPlayEnter(out string animationError))
                    return FailShow($"角色展示动画启动失败：{animationError}");

                double animationCompletedAt = Time.realtimeSinceStartupAsDouble;

                if (_logLoadTiming)
                {
                    Debug.Log(
                        $"[角色预览耗时] Address={normalizedAddress}, " +
                        $"Load={(loadCompletedAt - loadStartedAt) * 1000d:F1}ms, " +
                        $"Instantiate={(instantiateCompletedAt - instantiateStartedAt) * 1000d:F1}ms, " +
                        $"Animation={(animationCompletedAt - instantiateCompletedAt) * 1000d:F1}ms, " +
                        $"Total={(animationCompletedAt - totalStartedAt) * 1000d:F1}ms",
                        this);
                }

                // 首次加载成功后由缓存接管 Lease，切换角色时不再释放底层 Handle
                if (!candidateLeaseOwnedByCache)
                {
                    _previewAssetCache[normalizedAddress] = candidateLease;
                    candidateLeaseOwnedByCache = true;
                }

                // 候选对象完全可用后才清理旧预览
                ReleaseCurrentPreview();

                _previewInstance = candidateInstance;
                _previewAnimation = candidateAnimation;
                _sourceAddress = normalizedAddress;

                candidateInstance = null;
                candidateAnimation = null;
                candidateLease = null;

                LastError = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                return FailShow($"创建角色预览时发生异常：{exception.Message}");
            }
            finally
            {
                ReleasePreview(candidateInstance, candidateAnimation);

                // 缓存拥有的 Lease 只能在 Clear 时统一释放
                if (!candidateLeaseOwnedByCache)
                    candidateLease?.Dispose();

                if (requestVersion == _requestVersion)
                    IsLoading = false;
            }
        }

        /// <summary>
        /// 取消等待中的展示请求并释放当前实例与本次大厅会话缓存的角色资源
        /// 重复调用保持安全
        /// </summary>
        public void Clear()
        {
            _requestVersion++;
            IsLoading = false;
            LastError = string.Empty;

            ReleaseCurrentPreview();
            ReleaseCachedPreviewAssets();
        }

        #endregion

        #region Release(资源释放)

        private void ReleaseCurrentPreview()
        {
            GameObject instance = _previewInstance;
            ICharacterPreview animation = _previewAnimation;

            // 先清空记录，避免清理回调重复处理同一对象
            _previewInstance = null;
            _previewAnimation = null;
            _sourceAddress = string.Empty;

            ReleasePreview(instance, animation);
        }

        /// <summary>
        /// 释放本次大厅会话中已经查看过的全部角色资源
        /// 当前展示实例必须先停止并销毁
        /// </summary>
        private void ReleaseCachedPreviewAssets()
        {
            foreach (IResourceLease<GameObject> lease in _previewAssetCache.Values)
            {
                lease?.Dispose();
            }

            _previewAssetCache.Clear();
        }

        /// <summary>
        /// 停止展示动画并销毁实例
        /// 资源 Lease 由大厅会话缓存统一管理
        /// </summary>
        private static void ReleasePreview(
            GameObject instance,
            ICharacterPreview animation)
        {
            if (animation is UnityEngine.Object animationObject && animationObject != null)
                animation.Stop();

            if (instance != null)
            {
                // Destroy 在帧末执行，先隐藏避免旧模型短暂残留
                instance.SetActive(false);
                Destroy(instance);
            }
        }

        #endregion

        #region Helpers

        private bool FailShow(string message)
        {
            LastError = string.IsNullOrWhiteSpace(message) ? "角色预览加载失败" : message.Trim();

            return false;
        }

        #endregion
    }
}

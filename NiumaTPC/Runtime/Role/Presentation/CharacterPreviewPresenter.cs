using System;
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

        #endregion

        #region Runtime State

        private GameObject _previewInstance;
        private ICharacterPreview _previewAnimation;
        private IResourceLease<GameObject> _previewLease;

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
        /// 快速切换时只有最后一次请求可以替换当前预览
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

            if (_previewInstance != null &&
                _previewLease != null &&
                !_previewLease.IsReleased &&
                string.Equals(_sourceAddress, normalizedAddress, StringComparison.Ordinal))
            {
                return true;
            }

            IsLoading = true;

            IResourceLease<GameObject> candidateLease = null;
            GameObject candidateInstance = null;
            ICharacterPreview candidateAnimation = null;

            try
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
                    return FailShow( $"角色预览资源加载失败 Code={loadResult.ErrorCode}, " + $"Message={loadResult.ErrorMessage}");
                }

                candidateLease = loadResult.Lease;

                GameObject prefab = candidateLease.Asset;
                candidateInstance = Instantiate(prefab, _previewRoot, false);
                candidateInstance.transform.localPosition = Vector3.zero;
                candidateInstance.transform.localRotation = Quaternion.identity;

                // 展示动画组件约定位于预制体根节点
                candidateAnimation = candidateInstance.GetComponent<ICharacterPreview>();

                if (candidateAnimation == null)
                {
                    return FailShow( $"展示资源 {normalizedAddress} 的根节点缺少 CharacterAnimationPlay");
                }

                if (!candidateAnimation.TryPlayEnter(out string animationError))
                {
                    return FailShow($"角色展示动画启动失败：{animationError}");
                }

                // 候选对象完全可用后才清理旧预览
                ReleaseCurrentPreview();

                _previewInstance = candidateInstance;
                _previewAnimation = candidateAnimation;
                _previewLease = candidateLease;
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
                ReleasePreview(candidateInstance, candidateAnimation, candidateLease);

                if (requestVersion == _requestVersion)
                    IsLoading = false;
            }
        }

        /// <summary>
        /// 取消等待中的展示请求并释放当前实例与资源 Lease
        /// 重复调用保持安全
        /// </summary>
        public void Clear()
        {
            _requestVersion++;
            IsLoading = false;
            LastError = string.Empty;

            ReleaseCurrentPreview();
        }

        #endregion

        #region Release

        private void ReleaseCurrentPreview()
        {
            GameObject instance = _previewInstance;
            ICharacterPreview animation = _previewAnimation;
            IResourceLease<GameObject> lease = _previewLease;

            // 先清空记录，避免清理回调重复处理同一对象
            _previewInstance = null;
            _previewAnimation = null;
            _previewLease = null;
            _sourceAddress = string.Empty;

            ReleasePreview(instance, animation, lease);
        }

        /// <summary>
        /// 停止展示动画、销毁实例并归还对应资源 Lease
        /// 候选对象失败时不会影响当前已确认的预览
        /// </summary>
        private static void ReleasePreview(
            GameObject instance,
            ICharacterPreview animation,
            IResourceLease<GameObject> lease)
        {
            if (animation is UnityEngine.Object animationObject && animationObject != null)
            {
                animation.Stop();
            }

            if (instance != null)
            {
                // Destroy 在帧末执行，先隐藏避免旧模型短暂残留
                instance.SetActive(false);
                Destroy(instance);
            }

            lease?.Dispose();
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
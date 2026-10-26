using UnityEngine;

namespace NiumaTPC.Role
{
    /// <summary>
    /// 管理展示用角色实例
    /// (这里传入的是展示预制体)
    /// </summary>
    [DisallowMultipleComponent]
    public class CharacterPreviewPresenter : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("模型挂载位置")]
        private Transform _previewRoot;

        //组件自己创建的展示实例
        private GameObject _previewInstance;
        //记录当前实例来自哪个预制体
        private GameObject _sourcePrefab;

        // 当前展示实例的动画播放能力。
        private ICharacterPreview _previewAnimation;

        #region Unity生命周期

        private void OnDisable()
        {
            Clear();
        }

        #endregion

        /// <summary>
        /// 尝试展示角色
        /// </summary>
        public bool TryShow(GameObject prefab, out string error)
        {
            if (!isActiveAndEnabled)
            {
                error = "角色展示组件未启用";
                return false;
            }

            if (_previewRoot == null)
            {
                error = "没有绑定角色挂载点";
                return false;
            }

            if (prefab == null)
            {
                error = "角色展示预制体为空";
                return false;
            }

            if (_previewInstance != null && _sourcePrefab == prefab)
            {
                error = string.Empty;
                return true;
            }

            //TODO:后期可以制作一个对象池进行复用

            //创建实例
            GameObject instance = Instantiate(prefab, _previewRoot, false);
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;

            // 保留预制体自身的缩放，不强制修改 localScale
            // 约定播放组件位于展示预制体根节点
            ICharacterPreview animation = instance.GetComponent<ICharacterPreview>();

            if (animation == null)
            {
                ReleasePreview(instance, null);
                error = $"展示预制体 {prefab.name} 的根节点缺少 CharacterAnimationPlay 组件。";
                return false;
            }

            if (!animation.TryPlayEnter(out error))
            {
                // 失败时只释放候选实例，不误删当前正在展示的角色。
                ReleasePreview(instance, animation);
                error = $"角色展示动画启动失败：{error}";
                return false;
            }

            //预览确认可用后，再释放旧预览
            Clear();

            _previewInstance = instance;
            _sourcePrefab = prefab;
            _previewAnimation = animation;

            error = string.Empty;
            return true;
        }

        /// <summary>
        /// 清理当前展示预制体(实例化出来的)
        /// 不清除挂载点与资产
        /// </summary>
        public void Clear()
        {
            GameObject instance = _previewInstance;
            ICharacterPreview animation = _previewAnimation;
            
            // 先解除当前记录，避免清理触发其他回调时再次操作同一实例
            _previewInstance = null;
            _sourcePrefab = null;
            _previewAnimation = null;

            ReleasePreview(instance, animation);

        }


        /// <summary>
        /// 统一释放当前实例或创建失败的候选实例。
        /// 这里只接收本组件创建的场景实例。
        /// </summary>
        private static void ReleasePreview(
            GameObject instance,
            ICharacterPreview animation)
        {
            // 接口引用不会自动使用 Unity 的已销毁对象判空规则。
            if (animation is Object animationObject && animationObject != null)
            {
                // 返回 false 只表示之前没有正在进行的展示，不是清理失败。
                animation.Stop();
            }

            if (instance == null)
            {
                return;
            }

            // Destroy 在帧末执行，先隐藏，避免旧模型短暂残留。
            instance.SetActive(false);
            Destroy(instance);
        }


    }
}

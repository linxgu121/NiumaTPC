using UnityEngine;

namespace NiumaTPC.Item
{
    /// <summary>
    /// 将当前画面准星采样为射击请求
    /// 不读取按键、不校验装备、不扣弹，也不生成子弹
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WeaponFireRequestSource : MonoBehaviour
    {
        #region 场景引用

        [SerializeField]
        [Tooltip(
            "实际渲染本地玩家画面的透视 Camera，不能绑定 Cinemachine 虚拟相机" +
            "网络角色由 WeaponDriver 在采样前注入，预制体中可以留空")]
        private Camera aimCamera;

        #endregion

        #region 请求序号

        // 只记录请求生产编号，服务器的消费与去重记录仍由 WeaponDriver 管理
        // 不在 OnEnable 中清零，避免组件重新启用后重复使用旧编号
        private ulong _lastCreatedRequestSequence;

        private ulong CreateRequestSequence()
        {
            _lastCreatedRequestSequence =
                checked(_lastCreatedRequestSequence + 1UL);

            return _lastCreatedRequestSequence;
        }

        #endregion

        #region 相机注入

        /// <summary>
        /// 更新本地玩家实际使用的渲染相机
        /// </summary>
        public void SetAimCamera(Camera camera)
        {
            aimCamera = camera;
        }

        #endregion

        #region 请求采样

        /// <summary>
        /// 检查采样组件、透视相机和非零装备代次是否可用
        /// 此处只创建瞄准快照，不代表服务器允许此次射击
        /// </summary>
        public bool TryCreateRequest(
            uint equipmentRevision,
            out WeaponFireRequest request)
        {
            request = default;

            if (!isActiveAndEnabled ||
                aimCamera == null ||
                !aimCamera.isActiveAndEnabled ||
                aimCamera.orthographic ||
                equipmentRevision == 0u)
            {
                return false;
            }

            Ray aimRay = aimCamera.ViewportPointToRay(
                new Vector3(0.5f, 0.5f, 0f));

            // 保留现有准星射击规则
            // 起点使用相机位置，不使用位于近裁剪面上的 Ray.origin
            request = new WeaponFireRequest(
                CreateRequestSequence(),
                equipmentRevision,
                aimCamera.transform.position,
                aimRay.direction);

            return true;
        }

        #endregion
    }
}
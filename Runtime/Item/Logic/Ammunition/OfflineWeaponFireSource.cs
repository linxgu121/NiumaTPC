using NiumaTPC.Character;
using UnityEngine;

namespace NiumaTPC.Item
{
    /// <summary>
    /// 连接当前角色装备、相机与离线弹道世界
    /// 检查并提交武器状态，不读取输入，也不推进子弹
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NiumaCharacterController))]
    public class OfflineWeaponFireSource : MonoBehaviour
    {
        #region 场景引用

        [SerializeField]
        [Tooltip("实际渲染当前玩家画面的透视 Camera")]
        private Camera aimCamera;

        [SerializeField]
        [Tooltip("绑定场景中唯一负责本物理世界的 OfflineAmmunitionDriver")]
        private OfflineAmmunitionDriver ammunitionDriver;

        #endregion

        #region 角色引用

        private NiumaCharacterController _player;

        private void Awake()
        {
            _player = GetComponent<NiumaCharacterController>();
        }

        #endregion

        #region 引用注入

        public void Configure(
            Camera camera,
            OfflineAmmunitionDriver driver)
        {
            aimCamera = camera;
            ammunitionDriver = driver;
        }

        /// <summary>
        /// 只更新请求采样使用的相机，不改变弹道驱动引用
        /// </summary>
        public void SetAimCamera(Camera camera)
        {
            aimCamera = camera;
        }

        #endregion

        #region 请求创建与序号

        // 请求生产侧：为新的开火尝试分配编号
        private ulong _lastCreatedRequestSequence;

        // 请求处理侧：记录已消费的最大编号
        private ulong _lastProcessedRequestSequence;

        private ulong CreateRequestSequence()
        {
            _lastCreatedRequestSequence = checked(_lastCreatedRequestSequence + 1UL);

            return _lastCreatedRequestSequence;
        }

        /// <summary>
        /// 采样本次开火意图，不生成子弹，也不消耗弹药
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

            Ray aimRay = aimCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

            // 保持相机位置起射，不使用近裁剪面上的 Ray.origin
            request = new WeaponFireRequest(
                CreateRequestSequence(),
                equipmentRevision,
                aimCamera.transform.position,
                aimRay.direction);

            return true;
        }

        #endregion


        #region 发射入口

        public bool TryFire(
            ItemInstance weaponInstance,
            WeaponFireRequest request,
            out ulong shotId,
            out WeaponFireRejectReason rejectReason)
        {
            shotId = 0;
            rejectReason = WeaponFireRejectReason.None;

            ulong requestSequence = request.RequestSequence;
            uint equipmentRevision = request.EquipmentRevision;

            if (requestSequence == 0UL)
            {
                rejectReason = WeaponFireRejectReason.InvalidRequestSequence;
                return false;
            }

            if (requestSequence <= _lastProcessedRequestSequence)
            {
                rejectReason = WeaponFireRejectReason.StaleRequest;
                return false;
            }

            // 新请求先消费编号，业务拒绝后也不能再次重放
            _lastProcessedRequestSequence = requestSequence;

            if (!isActiveAndEnabled ||
                _player == null ||
                !_player.isActiveAndEnabled ||
                _player.RuntimeData == null ||
                ammunitionDriver == null ||
                !ammunitionDriver.isActiveAndEnabled)
            {
                rejectReason = WeaponFireRejectReason.Unavailable;
                return false;
            }

            // 在登记子弹和消耗弹药之前，检查请求中的瞄准数据
            if (!request.TryGetAimRay(out Ray aimRay))
            {
                rejectReason = WeaponFireRejectReason.InvalidAim;
                return false;
            }

            var data = _player.RuntimeData;

            if (data.IsDead || data.Arbitration.IsDead)
            {
                rejectReason = WeaponFireRejectReason.Dead;
                return false;
            }

            if (data.Arbitration.BlockInput ||
                data.Arbitration.BlockUpperBody)
            {
                rejectReason = WeaponFireRejectReason.ActionBlocked;
                return false;
            }

            // 保持当前必须瞄准才能开火的规则
            if (!data.IsAiming)
            {
                rejectReason = WeaponFireRejectReason.NotAiming;
                return false;
            }

            // 实例与装备代次必须同时匹配
            if (weaponInstance == null ||
                weaponInstance.CurrentAmount <= 0 ||
                data.CurrentItem != weaponInstance ||
                equipmentRevision == 0u ||
                data.EquipmentRevision != equipmentRevision)
            {
                rejectReason = WeaponFireRejectReason.EquipmentMismatch;
                return false;
            }

            RangedWeaponSO weapon = weaponInstance.BaseData as RangedWeaponSO;

            RangedWeaponRuntimeState state = weaponInstance.WeaponState;

            if (weapon == null || state == null)
            {
                rejectReason = WeaponFireRejectReason.InvalidWeapon;
                return false;
            }

            // 同一次处理使用同一个时间样本
            double now = Time.timeAsDouble;

            if (!state.CanFire(now))
            {
                // 仍由武器状态统一判断，这里只区分失败原因
                rejectReason = state.AmmoInMagazine <= 0
                    ? WeaponFireRejectReason.EmptyMagazine
                    : WeaponFireRejectReason.Cooldown;

                return false;
            }

            double fireInterval = Mathf.Max(0.001f, weapon.FireRate);

            // 使用创建请求时的瞄准快照
            if (!ammunitionDriver.TryFire(
                weapon,
                aimRay.origin,
                aimRay.direction,
                transform,
                out shotId))
            {
                rejectReason = WeaponFireRejectReason.SpawnFailed;
                return false;
            }

            // 只有逻辑子弹登记成功，才消耗弹药并更新冷却
            state.CommitAcceptedShot(now, fireInterval);

            return true;
        }

        #endregion
    }
}
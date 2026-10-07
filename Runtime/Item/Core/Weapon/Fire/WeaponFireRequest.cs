using UnityEngine;

namespace NiumaTPC.Item
{
    /// <summary>
    /// 一次开火尝试的输入快照
    /// 不携带武器配置、弹量或伤害等权威数据
    /// </summary>
    public struct WeaponFireRequest
    {
        // 同一角色发射入口内递增的请求编号
        public ulong RequestSequence;

        // 发起请求时的装备代次
        public uint EquipmentRevision;

        // 发起请求时的逻辑起点，使用世界坐标
        public Vector3 AimOrigin;

        // 发起请求时的准星射线方向，使用世界方向
        public Vector3 AimDirection;

        public WeaponFireRequest(
            ulong requestSequence,
            uint equipmentRevision,
            Vector3 aimOrigin,
            Vector3 aimDirection)
        {
            RequestSequence = requestSequence;
            EquipmentRevision = equipmentRevision;
            AimOrigin = aimOrigin;
            AimDirection = aimDirection;
        }

        #region 瞄准数据检查

        /// <summary>
        /// 检查请求中的瞄准数值，并返回方向归一化后的射线
        /// 这里只检查数值，不代表服务器已经认可这个视点
        /// </summary>
        public bool TryGetAimRay(out Ray aimRay)
        {
            aimRay = default;

            if (!IsFinite(AimOrigin) || !IsFinite(AimDirection))
            {
                return false;
            }

            float directionLengthSquared = AimDirection.sqrMagnitude;

            // 分量有限，长度平方仍可能因乘法溢出而成为无穷大
            // 最小方向阈值与现有弹道驱动保持一致
            if (float.IsInfinity(directionLengthSquared) ||
                directionLengthSquared <= 0.000001f)
            {
                return false;
            }

            // 保留请求原始起点，不重新读取相机或模型枪口
            aimRay = new Ray(AimOrigin, AimDirection.normalized);
            return true;
        }

        private static bool IsFinite(Vector3 value)
        {
            return !float.IsNaN(value.x) &&
                   !float.IsInfinity(value.x) &&
                   !float.IsNaN(value.y) &&
                   !float.IsInfinity(value.y) &&
                   !float.IsNaN(value.z) &&
                   !float.IsInfinity(value.z);
        }

        #endregion
    }
}
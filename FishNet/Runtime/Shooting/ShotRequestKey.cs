using System;

namespace NiumaTPC.FishNet
{
    /// <summary>
    /// 关联一次开火请求与服务器子弹
    /// 武器实例 ID 必须来自服务器确认记录
    /// </summary>
    public readonly struct ShotRequestKey : IEquatable<ShotRequestKey>
    {
        public readonly string ServerWeaponInstanceId;
        public readonly uint EquipmentRevision;
        public readonly ulong RequestSequence;

        public bool IsValid =>
            !string.IsNullOrEmpty(ServerWeaponInstanceId) &&
            EquipmentRevision != 0u &&
            RequestSequence != 0UL;

        public ShotRequestKey(
            string serverWeaponInstanceId,
            uint equipmentRevision,
            ulong requestSequence)
        {
            ServerWeaponInstanceId =
                serverWeaponInstanceId ?? string.Empty;

            EquipmentRevision = equipmentRevision;
            RequestSequence = requestSequence;
        }

        #region 字典键比较

        public bool Equals(ShotRequestKey other)
        {
            return string.Equals(
                       ServerWeaponInstanceId,
                       other.ServerWeaponInstanceId,
                       StringComparison.Ordinal) &&
                   EquipmentRevision == other.EquipmentRevision &&
                   RequestSequence == other.RequestSequence;
        }

        public override bool Equals(object obj)
        {
            return obj is ShotRequestKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = StringComparer.Ordinal.GetHashCode(
                    ServerWeaponInstanceId ?? string.Empty);

                hash = hash * 397 ^ EquipmentRevision.GetHashCode();
                hash = hash * 397 ^ RequestSequence.GetHashCode();
                return hash;
            }
        }

        #endregion
    }
}
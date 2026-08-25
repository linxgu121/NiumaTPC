using UnityEngine;

namespace NiumaTPC.Character.Simulation
{
    /// <summary>
    /// 把世界移动方向转换成相对角色朝向的八方向起步编号。
    /// 分区规则与原 PlayerMoveStartState 保持一致。
    /// </summary>
    public static class CharacterStartDirectionResolver
    {
        #region Constants

        private const float SectorAngle = 45f;
        private const float HalfSectorAngle = SectorAngle * 0.5f;

        #endregion

        #region Public API

        public static CharacterStartDirection Resolve(
            float characterYaw,
            in Vector3 worldDirection)
        {
            if (worldDirection.sqrMagnitude <= 0.0001f)
            {
                return CharacterStartDirection.Forward;
            }

            float targetYaw =Mathf.Atan2(worldDirection.x, worldDirection.z) * Mathf.Rad2Deg;

            // 计算移动方向相对于角色当前正面的角度。
            float localAngle =
                Mathf.DeltaAngle(characterYaw, targetYaw);

            return ResolveLocalAngle(localAngle);
        }

        #endregion

        #region Direction Quantization(方向量化)

        /// <summary>
        /// 把角色局部移动角度量化为八方向编号。
        /// 动画选择和模拟曲线索引必须共用同一套边界规则。
        /// </summary>
        public static CharacterStartDirection ResolveLocalAngle(float angle)
        {
            if (angle > -HalfSectorAngle && angle <= HalfSectorAngle)
            {
                return CharacterStartDirection.Forward;
            }

            if (angle > HalfSectorAngle && angle <= HalfSectorAngle + SectorAngle)
            {
                return CharacterStartDirection.ForwardRight;
            }

            if (angle > HalfSectorAngle + SectorAngle && angle <= HalfSectorAngle + SectorAngle * 2f)
            {
                return CharacterStartDirection.Right;
            }

            if (angle > HalfSectorAngle + SectorAngle * 2f && angle <= 180f - HalfSectorAngle)
            {
                return CharacterStartDirection.BackRight;
            }

            if (angle > 180f - HalfSectorAngle || angle <= -180f + HalfSectorAngle)
            {
                return CharacterStartDirection.Back;
            }

            if (angle > -180f + HalfSectorAngle && angle <= -HalfSectorAngle - SectorAngle * 2f)
            {
                return CharacterStartDirection.BackLeft;
            }

            if (angle > -HalfSectorAngle - SectorAngle * 2f && angle <= -HalfSectorAngle - SectorAngle)
            {
                return CharacterStartDirection.Left;
            }

            return CharacterStartDirection.ForwardLeft;
        }

        #endregion
    }
}

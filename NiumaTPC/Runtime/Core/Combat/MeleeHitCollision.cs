using System.Collections.Generic;
using UnityEngine;

namespace NiumaTPC.Core.Combat
{
    /// <summary>
    /// 近战攻击盒查询结果
    /// </summary>
    public enum MeleeHitCollisionResult : byte
    {
        /// <summary>
        /// 查询参数或物理场景无效
        /// </summary>
        Invalid = 0,
        /// <summary>
        /// 查询成功但没有发现受击区域
        /// </summary>
        Clear = 1,
        /// <summary>
        /// 查询成功并发现至少一个受击区域
        /// </summary>
        Hit = 2,
        /// <summary>
        /// 查询缓冲区已满，结果可能不完整
        /// </summary>
        Overflow = 3
    }

    /// <summary>
    /// 将角色局部攻击盒转换到世界空间并查询 DamageHurtbox
    /// 不负责攻击时机、目标去重或伤害结算
    /// </summary>
    public sealed class MeleeHitCollision
    {
        #region Constants

        // 固定容量可以避免在战斗 Tick 中发生数组扩容
        private const int HitCapacity = 32;

        #endregion

        #region Runtime State

        private readonly Collider[] _overlapColliders = new Collider[HitCapacity];

        #endregion

        #region Public API

        /// <summary>
        /// 检查攻击盒配置并查询服务器物理场景中的受击区域
        /// 查询结果装满缓冲区时返回 Overflow，不提交不完整候选
        /// </summary>
        public MeleeHitCollisionResult QueryBox(
            PhysicsScene physicsScene,
            Vector3 actorPosition,
            float actorYaw,
            Vector3 localCenter,
            Vector3 halfExtents,
            LayerMask hurtboxLayers,
            List<DamageHurtbox> results,
            out Vector3 worldCenter)
        {
            worldCenter = Vector3.zero;

            if (results == null)
            {
                return MeleeHitCollisionResult.Invalid;
            }

            results.Clear();

            if (!physicsScene.IsValid() ||
                hurtboxLayers.value == 0 ||
                !IsFinite(actorPosition) ||
                !IsFinite(actorYaw) ||
                !IsFinite(localCenter) ||
                !IsPositiveFinite(halfExtents))
            {
                return MeleeHitCollisionResult.Invalid;
            }

            Quaternion actorRotation = Quaternion.Euler(0f, actorYaw, 0f);

            worldCenter = actorPosition + actorRotation * localCenter;

            int overlapCount = physicsScene.OverlapBox(
                worldCenter,
                halfExtents,
                _overlapColliders,
                actorRotation,
                hurtboxLayers.value,
                QueryTriggerInteraction.Collide);

            /*
             * 数组刚好装满时无法确认是否还有遗漏
             * 不把不完整结果交给后续权威判定
             */
            if (overlapCount >= _overlapColliders.Length)
            {
                results.Clear();
                return MeleeHitCollisionResult.Overflow;
            }

            for (int i = 0; i < overlapCount; i++)
            {
                Collider candidate = _overlapColliders[i];

                if (candidate == null ||!candidate.TryGetComponent(out DamageHurtbox hurtbox))
                {
                    continue;
                }

                results.Add(hurtbox);
            }

            return results.Count > 0 ? MeleeHitCollisionResult.Hit : MeleeHitCollisionResult.Clear;
        }

        #endregion

        #region Validation

        // 检查数值是否为有限值
        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        // 检查向量三个分量是否都是有限值
        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        // 检查向量三个分量是否都是大于零的有限值
        private static bool IsPositiveFinite(Vector3 value)
        {
            return IsFinite(value) && value.x > 0f && value.y > 0f && value.z > 0f;
        }

        #endregion
    }
}
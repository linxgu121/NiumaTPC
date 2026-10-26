using System;
using NiumaTPC.Core.Combat;
using UnityEngine;

namespace NiumaTPC.Character.Combat
{
    /// <summary>
    /// 检查攻击者与近战候选之间是否存在物理阻挡
    /// 不负责候选排序、吸附位移或伤害结算
    /// </summary>
    public sealed class MeleeTargetLineOfSightChecker
    {
        private const int InitialHitCapacity = 16;
        private const int MaxHitCapacity = 1024;
        private const float MinimumDistance = 0.0001f;

        // 复用查询数组，只有容量不足时才扩容
        private RaycastHit[] _hits = new RaycastHit[InitialHitCapacity];

        /// <summary>
        /// 检查攻击者到目标之间最近的非自身碰撞是否属于目标
        /// 查询溢出或没有确认命中目标时按不可见处理
        /// </summary>
        public bool HasLineOfSight(
            PhysicsScene physicsScene,
            Transform attackerRoot,
            Vector3 origin,
            IMeleeTarget target,
            LayerMask collisionMask,
            float castRadius,
            QueryTriggerInteraction triggerInteraction,
            out RaycastHit firstHit)
        {
            firstHit = default;

            if (!physicsScene.IsValid() ||
                attackerRoot == null ||
                IsMissingTarget(target))
            {
                return false;
            }

            Transform targetRoot = target.RootTransform;

            if (targetRoot == null)
            {
                return false;
            }

            Vector3 displacement = target.AdsorptionPosition - origin;

            float distance = displacement.magnitude;

            if (distance <= MinimumDistance)
            {
                return true;
            }

            Vector3 direction = displacement / distance;
            float safeRadius = Mathf.Max(0f, castRadius);
            int hitCount;

            while (true)
            {
                if (safeRadius > 0f)
                {
                    hitCount = physicsScene.SphereCast(
                        origin,
                        safeRadius,
                        direction,
                        _hits,
                        distance,
                        collisionMask.value,
                        triggerInteraction);
                }
                else
                {
                    hitCount = physicsScene.Raycast(
                        origin,
                        direction,
                        _hits,
                        distance,
                        collisionMask.value,
                        triggerInteraction);
                }

                if (hitCount < _hits.Length)
                {
                    break;
                }

                if (_hits.Length >= MaxHitCapacity)
                {
                    // 无法确认是否遗漏了更近的墙体，按阻挡处理
                    return false;
                }

                int newCapacity = Mathf.Min(
                    _hits.Length * 2,
                    MaxHitCapacity);

                Array.Resize(ref _hits, newCapacity);
            }

            bool foundHit = false;
            float closestDistance = float.PositiveInfinity;

            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit candidate = _hits[i];
                Collider hitCollider = candidate.collider;

                if (hitCollider == null)
                {
                    continue;
                }

                Transform hitTransform = hitCollider.transform;

                if (BelongsToRoot(hitTransform,attackerRoot))
                {
                    continue;
                }

                if (candidate.distance >= closestDistance)
                {
                    continue;
                }

                closestDistance = candidate.distance;
                firstHit = candidate;
                foundHit = true;
            }

            if (!foundHit || firstHit.collider == null)
            {
                return false;
            }

            return BelongsToRoot(
                firstHit.collider.transform,
                targetRoot);
        }

        /// <summary>
        /// 检查目标接口是否为空或对应的 Unity 对象已经销毁
        /// </summary>
        private static bool IsMissingTarget(
            IMeleeTarget target)
        {
            if (target == null)
            {
                return true;
            }

            return target is UnityEngine.Object unityObject && unityObject == null;
        }

        /// <summary>
        /// 检查碰撞体节点是否属于指定角色根节点
        /// </summary>
        private static bool BelongsToRoot(
            Transform candidate,
            Transform root)
        {
            if (candidate == null || root == null)
            {
                return false;
            }

            return candidate == root || candidate.IsChildOf(root);
        }
    }
}
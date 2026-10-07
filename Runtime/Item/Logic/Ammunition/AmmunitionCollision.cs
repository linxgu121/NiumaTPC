using System;
using UnityEngine;

namespace NiumaTPC.Item
{
    /// <summary>
    /// 查询单段弹道上的最近有效命中
    /// </summary>
    public class AmmunitionCollision
    {
        // 碰撞数组初始容量
        private const int InitialHitCapacity = 16;
        // 数组扩容上限
        private const int MaxHitCapacity = 1024;

        // 顺序处理不同子弹时复用，避免每次查询都创建数组
        private RaycastHit[] _hits = new RaycastHit[InitialHitCapacity];

        // 点状弹起点检查使用的安全半径，单位米
        // 不改变飞行过程中使用 Raycast 的规则
        private const float PointOverlapRadius = 0.001f;

        // 重叠查询返回 Collider，而不是 RaycastHit
        private Collider[] _overlapColliders = new Collider[InitialHitCapacity];


        /// <summary>
        /// 查询当前位置到候选位置之间的有效阻挡
        /// shooterRoot 应为射手角色根节点，不能传场景公共父节点
        /// </summary>
        public AmmunitionCastResult Cast(
            PhysicsScene physicsScene,
            in AmmunitionState state,
            Vector3 nextPosition,
            Transform shooterRoot,
            LayerMask hitMask,
            QueryTriggerInteraction triggerInteraction,
            out RaycastHit closestHit)
        {
            closestHit = default;

            // 子弹本步要移动的矢量
            Vector3 displacement = nextPosition - state.Position;
            // 位移向量长度，也就是起点到终点的直线距离
            float distance = displacement.magnitude;

            // 没有移动线段。本方法不承担起点重叠检测
            if (distance <= 0f)
            {
                return AmmunitionCastResult.Clear;
            }

            // 单位方向向量
            Vector3 direction = displacement / distance;
            // 保存物理查询返回的碰撞数量
            int hitCount;

            // 数组装满时，不能确定是否还有命中被遗漏
            while (true)
            {
                if (state.CollisionRadius > 0f)
                {
                    hitCount = physicsScene.SphereCast(
                        state.Position,
                        state.CollisionRadius,
                        direction,
                        _hits,
                        distance,
                        hitMask.value,
                        triggerInteraction);
                }
                else
                {
                    // 点状子弹使用射线，不使用半径为零的球形扫掠。
                    hitCount = physicsScene.Raycast(
                        state.Position,
                        direction,
                        _hits,
                        distance,
                        hitMask.value,
                        triggerInteraction);
                }

                if (hitCount < _hits.Length)
                {
                    break;
                }

                if (_hits.Length >= MaxHitCapacity)
                {
                    return AmmunitionCastResult.Overflow;
                }

                int newCapacity = Mathf.Min(
                    _hits.Length * 2,
                    MaxHitCapacity);

                // 仅扩容时分配；后续查询继续复用扩大后的数组
                Array.Resize(ref _hits, newCapacity);
            }

            bool found = false;
            float closestDistance = float.PositiveInfinity;

            // 结果不保证按距离排序，需要自己选择最近有效命中。
            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit candidate = _hits[i];
                Collider hitCollider = candidate.collider;

                if (hitCollider == null)
                {
                    continue;
                }

                // 使用 Collider 自身的 Transform 判断归属。
                Transform hitTransform = hitCollider.transform;

                // 判定为击中自己，跳过本次碰撞
                if (shooterRoot != null &&
                    (hitTransform == shooterRoot ||
                     hitTransform.IsChildOf(shooterRoot)))
                {
                    continue;
                }

                // 如果当前候选命中距离 大于等于已经记录的最近距离，跳过，不更新
                if (candidate.distance >= closestDistance)
                {
                    continue;
                }

                closestDistance = candidate.distance;
                closestHit = candidate;
                found = true;
            }

            return found ? AmmunitionCastResult.Hit : AmmunitionCastResult.Clear;
        }

        /// <summary>
        /// 检查逻辑弹道起点是否与有效阻挡重叠
        /// Hit 仅表示发现重叠，不代表可以结算一次普通命中
        /// </summary>
        public AmmunitionCastResult CheckInitialOverlap(
            PhysicsScene physicsScene,
            in AmmunitionState state,
            Transform shooterRoot,
            LayerMask hitMask,
            QueryTriggerInteraction triggerInteraction,
            out Collider blockingCollider)
        {
            blockingCollider = null;

            float radius = state.CollisionRadius > 0f
                ? state.CollisionRadius
                : PointOverlapRadius;

            while (true)
            {
                int overlapCount = physicsScene.OverlapSphere(
                    state.Position,
                    radius,
                    _overlapColliders,
                    hitMask.value,
                    triggerInteraction);

                for (int i = 0; i < overlapCount; i++)
                {
                    Collider candidate = _overlapColliders[i];

                    if (candidate == null)
                    {
                        continue;
                    }

                    Transform candidateTransform = candidate.transform;

                    // 射手自身不作为阻挡
                    if (shooterRoot != null &&
                        (candidateTransform == shooterRoot ||
                         candidateTransform.IsChildOf(shooterRoot)))
                    {
                        continue;
                    }

                    // 这里只需确认存在阻挡，不需要选择最近目标
                    blockingCollider = candidate;
                    return AmmunitionCastResult.Hit;
                }

                // 结果没有装满，且全部被过滤，才能确认起点检查通过
                if (overlapCount < _overlapColliders.Length)
                {
                    return AmmunitionCastResult.Clear;
                }

                // 结果装满且没有找到有效阻挡，仍可能遗漏其他碰撞体
                if (_overlapColliders.Length >= MaxHitCapacity)
                {
                    return AmmunitionCastResult.Overflow;
                }

                int newCapacity = Mathf.Min(
                    _overlapColliders.Length * 2,
                    MaxHitCapacity);

                Array.Resize(ref _overlapColliders, newCapacity);
            }
        }


    }
}

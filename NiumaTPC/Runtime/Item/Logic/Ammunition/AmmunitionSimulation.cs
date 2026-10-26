using UnityEngine;

namespace NiumaTPC.Item
{
    /// <summary>
    /// 串联单颗子弹的运动计算、碰撞查询与状态提交
    /// </summary>
    public class AmmunitionSimulation
    {
        // 不同子弹顺序复用同一个查询器及其命中缓冲区
        private readonly AmmunitionCollision _collision = new AmmunitionCollision();

        // 单个子步的飞行路程上限，单位米
        private const float MaxStepDistance = 5f;

        // 恒加速度曲线与本段直线近似的允许位置误差，单位米
        private const float MaxCurveError = 0.001f;

        // 单颗子弹在一个 Tick 内最多执行的子步数量
        private const int MaxSubstepsPerTick = 32;

        /// <summary>
        /// 将一个 Tick 拆成若干子步，依次推进同一颗子弹
        /// 返回 true 仅表示本次 Tick 新发生了命中
        /// </summary>
        public bool SimulateTick(
            PhysicsScene physicsScene,
            ref AmmunitionState state,
            float deltaTime,
            Transform shooterRoot,
            LayerMask hitMask,
            QueryTriggerInteraction triggerInteraction,
            out RaycastHit hit)
        {
            hit = default;

            if (!state.IsAlive || deltaTime <= 0f)
            {
                return false;
            }

            // 整个 Tick 只记录一次，不能在子步中反复覆盖
            state.PreviousPosition = state.Position;

            float tickStartAge = state.Age;
            float remainingLifetime = state.MaxLifetime - tickStartAge;

            if (remainingLifetime <= 0f)
            {
                state.Age = state.MaxLifetime;
                state.Status = AmmunitionStatus.Expired;
                return false;
            }

            // 构造时 Age 为 0，第一次实际推进前检查逻辑起点
            if (state.Age == 0f)
            {
                AmmunitionCastResult overlapResult =
                    _collision.CheckInitialOverlap(
                        physicsScene,
                        in state,
                        shooterRoot,
                        hitMask,
                        triggerInteraction,
                        out Collider blockingCollider);

                if (overlapResult != AmmunitionCastResult.Clear)
                {
                    // 重叠或无法确认安全时，不移动、不产生伤害命中
                    state.Status = AmmunitionStatus.Cancelled;

                    if (overlapResult == AmmunitionCastResult.Overflow)
                    {
                        Debug.LogWarning(
                            $"[脚本弹道] 起点重叠查询溢出，取消子弹：" +
                            $"ShotId={state.ShotId}");
                    }
                    else
                    {
                        Debug.LogWarning(
                            $"[脚本弹道] 起点与阻挡物重叠，取消子弹：" +
                            $"ShotId={state.ShotId}, Collider={blockingCollider.name}");
                    }

                    return false;
                }
            }

            // 本 Tick 不能推进超过剩余寿命的时间
            float duration = Mathf.Min(deltaTime, remainingLifetime);

            // 恒加速度下，起末速度模长的较大值可作为本段速度上界
            Vector3 endVelocity = state.Velocity + state.Acceleration * duration;

            float maxSpeed = Mathf.Max( state.Velocity.magnitude,endVelocity.magnitude);

            // 根据每段最大路程计算所需分段数
            float stepsByDistance = maxSpeed * duration / MaxStepDistance;

            // 时长 h 的子步，其曲线与端点线性插值的最大偏差为 |a|h²/8
            // 将总时长平均分成 N 段后，偏差会缩小为原来的 1/N²
            float stepsByCurve = Mathf.Sqrt(
                state.Acceleration.magnitude * duration * duration /
                (8f * MaxCurveError));

            float requiredSteps = Mathf.Max(1f, Mathf.Max(stepsByDistance, stepsByCurve));

            // 先检查预算，再开始移动
            // 不能强行减少分段数后，仍声称满足原来的精度要求
            if (requiredSteps > MaxSubstepsPerTick)
            {
                state.Status = AmmunitionStatus.Cancelled;

                Debug.LogWarning(
                    $"[脚本弹道] 子步需求超过预算，取消子弹：" +
                    $"ShotId={state.ShotId}, Required={requiredSteps:F2}, " +
                    $"Limit={MaxSubstepsPerTick}");

                return false;
            }

            int stepCount = Mathf.CeilToInt(requiredSteps);
            float simulatedTime = 0f;

            for (int i = 0; i < stepCount; i++)
            {
                // 用时间边界求每段时长，最后一个边界为完整 duration
                float nextTime = duration * ((i + 1) / (float)stepCount);

                float substepDuration = nextTime - simulatedTime;

                if (SimulateStep(
                        physicsScene,
                        ref state,
                        substepDuration,
                        shooterRoot,
                        hitMask,
                        triggerInteraction,
                        out hit))
                {
                    // 命中后立即结束本 Tick，不继续推进剩余子步
                    return true;
                }

                if (!state.IsAlive)
                {
                    // 寿命到期或碰撞查询溢出，也必须立即停止
                    return false;
                }

                simulatedTime = nextTime;
            }

            // 所有子步正常完成后，按 Tick 总时长校正年龄
            state.Age = Mathf.Min(
                tickStartAge + duration,
                state.MaxLifetime);

            if (duration >= remainingLifetime ||
                state.Age >= state.MaxLifetime)
            {
                state.Age = state.MaxLifetime;
                state.Status = AmmunitionStatus.Expired;
            }

            return false;
        }


        /// <summary>
        /// 推进单颗子弹一个模拟子步
        /// 返回 true 仅表示本次调用新发生了命中
        /// 是否继续飞行应读取 state.IsAlive
        /// </summary>
        private bool SimulateStep(
            PhysicsScene physicsScene,
            ref AmmunitionState state,
            float deltaTime,
            Transform shooterRoot,
            LayerMask hitMask,
            QueryTriggerInteraction triggerInteraction,
            out RaycastHit hit)
        {
            hit = default;

            // 已结束的子弹不再次推进，也不重复报告命中
            if (!state.IsAlive || deltaTime <= 0f)
            {
                return false;
            }


            float remainingLifetime = state.MaxLifetime - state.Age;

            if (!AmmunitionMotion.TryCalculateStep(
                    in state,
                    deltaTime,
                    out float stepDuration,
                    out Vector3 nextPosition,
                    out Vector3 nextVelocity))
            {
                // 入口已排除非飞行状态和非正步长
                // 当前计算方法在这里返回 false，表示寿命已经耗尽
                state.Age = state.MaxLifetime;
                state.Status = AmmunitionStatus.Expired;
                return false;
            }

            // 此时只得到候选结果，还不能直接更新子弹位置
            AmmunitionCastResult castResult = _collision.Cast(
                physicsScene,
                in state,
                nextPosition,
                shooterRoot,
                hitMask,
                triggerInteraction,
                out RaycastHit candidateHit);

            if (castResult == AmmunitionCastResult.Overflow)
            {
                // 无法确认途中是否有阻挡，不允许继续飞行
                state.Status = AmmunitionStatus.Cancelled;

                Debug.LogWarning(
                    $"[脚本弹道] 碰撞查询溢出，取消子弹：ShotId={state.ShotId}");

                return false;
            }

            Vector3 displacement = nextPosition - state.Position;
            float segmentDistance = displacement.magnitude;

            if (castResult == AmmunitionCastResult.Hit)
            {
                float hitDistance = Mathf.Clamp(
                    candidateHit.distance,
                    0f,
                    segmentDistance);

                float hitFraction = segmentDistance > 0f
                    ? hitDistance / segmentDistance
                    : 0f;

                // 按本子步的路程比例估算命中时刻
                // 子步细分可降低误差，但仍不是精确的曲线交点计算
                float hitDuration = stepDuration * hitFraction;

                // 提交子弹中心的位置，而不是碰撞表面的接触点
                state.Position += displacement * hitFraction;

                state.Velocity += state.Acceleration * hitDuration;

                state.Age = Mathf.Min(
                    state.Age + hitDuration,
                    state.MaxLifetime);

                // 只累计到实际命中位置，不能累计完整候选路程
                state.TravelledDistance += hitDistance;

                // 先写入终态，再把本次命中交给调用方处理
                state.Status = AmmunitionStatus.Hit;
                hit = candidateHit;

                return true;
            }

            // 本段没有有效阻挡，可以提交完整候选结果
            state.Position = nextPosition;
            state.Velocity = nextVelocity;

            state.Age = Mathf.Min(
                state.Age + stepDuration,
                state.MaxLifetime);

            state.TravelledDistance += segmentDistance;

            // 最后一步结束时明确提交到期状态
            if (stepDuration >= remainingLifetime || state.Age >= state.MaxLifetime)
            {
                state.Age = state.MaxLifetime;
                state.Status = AmmunitionStatus.Expired;
            }

            return false;
        }

    }
}

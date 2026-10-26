using UnityEngine;

namespace NiumaTPC.Character.Simulation
{
    /// <summary>
    /// 计算近战吸附每个固定 Tick 的水平位移与朝向
    /// 不直接操作 CharacterController 或 FishNet
    /// </summary>
    public static class MeleeAdsorptionMovementSimulator
    {
        #region Constants

        /// <summary>
        /// 小于该距离时视为没有有效位移
        /// </summary>
        private const float DistanceEpsilon = 0.001f;

        /// <summary>
        /// 发生侧面碰撞且实际前进不足请求距离的一半时视为被阻挡
        /// </summary>
        private const float BlockedProgressRatio = 0.5f;

        #endregion

        #region Simulation

        /// <summary>
        /// 校验活动吸附状态并计算本 Tick 请求的水平位移与角色朝向
        /// 返回 true 表示本 Tick 的普通水平移动应被吸附接管
        /// </summary>
        public static bool TrySimulate(
            ref MeleeAdsorptionSimulationState state,
            Vector3 currentPosition,
            float currentYaw,
            float attackerCollisionRadius,
            float tickDeltaTime,
            out Vector3 horizontalDisplacement,
            out float nextYaw)
        {
            horizontalDisplacement = Vector3.zero;
            nextYaw = currentYaw;

            if (!state.IsActive)
            {
                return false;
            }

            if (!state.IsValid ||
                !IsFiniteVector(currentPosition) ||
                !IsFinite(currentYaw) ||
                !IsNonNegativeFinite(attackerCollisionRadius) ||
                !IsPositiveFinite(tickDeltaTime))
            {
                state.Stop();
                return false;
            }

            Vector3 toTarget =
                state.Request.TargetPosition -
                currentPosition;

            // 吸附只改变地面水平位置
            toTarget.y = 0f;

            float horizontalDistance =
                toTarget.magnitude;

            if (!IsFinite(horizontalDistance))
            {
                state.Stop();
                return false;
            }

            if (horizontalDistance <= DistanceEpsilon)
            {
                state.Stop();
                return true;
            }

            Vector3 direction =
                toTarget / horizontalDistance;

            float targetYaw =
                Mathf.Repeat(
                    Mathf.Atan2(
                        direction.x,
                        direction.z) *
                    Mathf.Rad2Deg,
                    360f);

            float maximumYawDelta =
                state.MotionProfile
                    .MaximumTurnSpeedDegreesPerSecond *
                tickDeltaTime;

            nextYaw = Mathf.MoveTowardsAngle(
                currentYaw,
                targetYaw,
                maximumYawDelta);

            float stoppingDistance =
                attackerCollisionRadius +
                state.Request.TargetCollisionRadius +
                state.MotionProfile.StoppingSurfaceGap;

            if (!IsFinite(stoppingDistance))
            {
                state.Stop();
                return false;
            }

            float distanceBeforeStop =
                Mathf.Max(
                    0f,
                    horizontalDistance -
                    stoppingDistance);

            float remainingTravelBudget =
                Mathf.Max(
                    0f,
                    state.Request.MaximumTravelDistance -
                    state.AccumulatedTravelDistance);

            /*
             * 已经到达停止距离或耗尽位移预算时，
             * 本 Tick 仍由吸附接管水平移动并应用最后一次朝向
             */
            if (distanceBeforeStop <= DistanceEpsilon ||
                remainingTravelBudget <= DistanceEpsilon)
            {
                state.Stop();
                return true;
            }

            float maximumTickDistance =
                state.MotionProfile.MaximumSpeed *
                tickDeltaTime;

            if (!IsPositiveFinite(maximumTickDistance))
            {
                state.Stop();
                return false;
            }

            float requestedDistance =
                Mathf.Min(
                    maximumTickDistance,
                    distanceBeforeStop,
                    remainingTravelBudget);

            if (requestedDistance <= DistanceEpsilon)
            {
                state.Stop();
                return true;
            }

            horizontalDisplacement =
                direction * requestedDistance;

            state.ElapsedTicks++;

            return true;
        }

        /// <summary>
        /// 根据碰撞身体的真实移动结果累计位移并判断吸附是否结束
        /// 撞墙、达到持续时间或耗尽总位移时立即停止
        /// </summary>
        public static void FinalizeAfterBodyMove(
            ref MeleeAdsorptionSimulationState state,
            in CharacterBodyMoveResult moveResult)
        {
            if (!state.IsActive)
            {
                return;
            }

            Vector3 actualHorizontal =
                moveResult.ActualDisplacement;

            actualHorizontal.y = 0f;

            if (!IsFiniteVector(actualHorizontal))
            {
                state.Stop();
                return;
            }

            float actualDistance =
                actualHorizontal.magnitude;

            if (!IsFinite(actualDistance))
            {
                state.Stop();
                return;
            }

            state.AccumulatedTravelDistance =
                Mathf.Min(
                    state.Request.MaximumTravelDistance,
                    state.AccumulatedTravelDistance +
                    actualDistance);

            bool blockedByWall =
                IsBlockedByWall(in moveResult);

            bool reachedDuration =
                state.ElapsedTicks >=
                state.MotionProfile.DurationTicks;

            bool exhaustedTravelBudget =
                state.AccumulatedTravelDistance >=
                state.Request.MaximumTravelDistance -
                DistanceEpsilon;

            if (blockedByWall ||
                reachedDuration ||
                exhaustedTravelBudget)
            {
                state.Stop();
            }
        }

        #endregion

        #region Collision

        /// <summary>
        /// 检查侧面碰撞是否明显阻止了本 Tick 朝目标方向前进
        /// 沿墙正常滑动且仍有足够前进量时不会误判为撞墙
        /// </summary>
        private static bool IsBlockedByWall(
            in CharacterBodyMoveResult moveResult)
        {
            if (!moveResult.CollidedSides)
            {
                return false;
            }

            Vector3 requestedHorizontal =
                moveResult.RequestedDisplacement;

            requestedHorizontal.y = 0f;

            float requestedDistance =
                requestedHorizontal.magnitude;

            if (requestedDistance <= DistanceEpsilon)
            {
                return false;
            }

            Vector3 requestedDirection =
                requestedHorizontal /
                requestedDistance;

            Vector3 actualHorizontal =
                moveResult.ActualDisplacement;

            actualHorizontal.y = 0f;

            float actualForwardDistance =
                Mathf.Max(
                    0f,
                    Vector3.Dot(
                        actualHorizontal,
                        requestedDirection));

            return
                actualForwardDistance <
                requestedDistance *
                BlockedProgressRatio;
        }

        #endregion

        #region Validation

        /// <summary>
        /// 检查三维向量是否全部为有限数值
        /// </summary>
        private static bool IsFiniteVector(Vector3 value)
        {
            return
                IsFinite(value.x) &&
                IsFinite(value.y) &&
                IsFinite(value.z);
        }

        /// <summary>
        /// 检查数值是否为大于零的有限值
        /// </summary>
        private static bool IsPositiveFinite(float value)
        {
            return IsFinite(value) && value > 0f;
        }

        /// <summary>
        /// 检查数值是否为非负有限值
        /// </summary>
        private static bool IsNonNegativeFinite(float value)
        {
            return IsFinite(value) && value >= 0f;
        }

        /// <summary>
        /// 检查数值是否可以安全用于角色模拟
        /// </summary>
        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        #endregion
    }
}
using UnityEngine;

namespace NiumaTPC.Item
{
    /// <summary>
    /// 子弹运动计算
    /// 负责计算候选位置和速度
    /// </summary>
    public static class AmmunitionMotion
    {
        /// <summary>
        /// 根据当前状态计算下一小步
        /// 实际推进时长不会超过子弹剩余寿命
        /// </summary>
        public static bool TryCalculateStep(
            in AmmunitionState state,
            float deltaTime,
            out float stepDuration,
            out Vector3 nextPosition,
            out Vector3 nextVelocity)
        {
            stepDuration = 0f;
            nextPosition = state.Position;
            nextVelocity = state.Velocity;

            // 已结束的子弹不继续计算，零步长也不产生推进
            if (!state.IsAlive || deltaTime <= 0f)
            {
                return false;
            }

            float remainingLifetime = state.MaxLifetime - state.Age;

            if (remainingLifetime <= 0f)
            {
                return false;
            }

            // 最后一步只计算剩余寿命，避免额外飞出一整 Tick
            stepDuration = Mathf.Min(deltaTime, remainingLifetime);

            float halfTimeSquared =
                0.5f * stepDuration * stepDuration;

            // 恒定加速度：位移 = 初速度 × 时间 + ½ × 加速度 × 时间²
            nextPosition = state.Position + state.Velocity * stepDuration + state.Acceleration * halfTimeSquared;

            // 当前速度随加速度变化
            nextVelocity = state.Velocity + state.Acceleration * stepDuration;

            return true;
        }
    }
}

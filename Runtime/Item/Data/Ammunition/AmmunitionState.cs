using UnityEngine;

namespace NiumaTPC.Item
{
    public struct AmmunitionState
    {
        /// <summary>
        /// 本次射击的唯一编号
        /// </summary>
        public readonly ulong ShotId;

        /// <summary>
        /// 逻辑弹道起点
        /// </summary>
        public readonly Vector3 StartPosition;

        /// <summary>
        /// 发射时确定的加速度
        /// 当前用于保存场景重力乘以弹药重力倍率的结果
        /// </summary>
        public readonly Vector3 Acceleration;

        /// <summary>
        /// 发射时保存的碰撞半径。
        /// </summary>
        public readonly float CollisionRadius;

        /// <summary>
        /// 发射时保存的最大存活时间。
        /// </summary>
        public readonly float MaxLifetime;

        /// <summary>
        /// 上一个模拟 Tick 的位置，供视觉插值使用
        /// 不根据渲染帧或模型 Transform 更新
        /// </summary>
        public Vector3 PreviousPosition;

        /// <summary>
        /// 当前逻辑位置。
        /// </summary>
        public Vector3 Position;

        /// <summary>
        /// 当前速度。
        /// 初始值由枪械提供，飞行中可被重力改变。
        /// </summary>
        public Vector3 Velocity;

        /// <summary>
        /// 已模拟的飞行时间，单位秒。
        /// </summary>
        public float Age;

        /// <summary>
        /// 累计实际飞行路程，供后续距离伤害规则使用。
        /// </summary>
        public float TravelledDistance;

        public AmmunitionStatus Status;

        public bool IsAlive => Status == AmmunitionStatus.Flying;

        public AmmunitionState(
            ulong shotId,
            Vector3 startPosition,
            Vector3 initialVelocity,
            Vector3 acceleration,
            float collisionRadius,
            float maxLifetime)
        {
            ShotId = shotId;
            StartPosition = startPosition;
            Acceleration = acceleration;
            CollisionRadius = collisionRadius;
            MaxLifetime = maxLifetime;

            PreviousPosition = startPosition;
            Position = startPosition;
            Velocity = initialVelocity;

            Age = 0f;
            TravelledDistance = 0f;
            Status = AmmunitionStatus.Flying;
        }

    }
}

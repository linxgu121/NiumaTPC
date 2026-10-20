using UnityEngine;

namespace NiumaTPC.Character.Simulation
{
    /// <summary>
    /// 固定 Tick 模拟使用的近战吸附请求
    /// 只保存客户端预测与服务器权威模拟都能复制的值数据
    /// </summary>
    public struct MeleeAdsorptionSimulationRequest
    {
        /// <summary>
        /// 攻击开始时的模拟 Tick
        /// uint 回绕后的 0 仍是合法值
        /// </summary>
        public uint AttackTick;

        /// <summary>
        /// 创建请求时采样的权威目标位置
        /// </summary>
        public Vector3 TargetPosition;

        /// <summary>
        /// 目标用于计算停止间隔的碰撞半径
        /// </summary>
        public float TargetCollisionRadius;

        /// <summary>
        /// 本次攻击允许吸附移动的最大距离
        /// </summary>
        public float MaximumTravelDistance;

        /// <summary>
        /// 检查吸附快照能否安全进入固定 Tick 模拟
        /// 默认结构体和包含非有限数值的请求均视为无效
        /// </summary>
        public bool IsValid =>
            IsFiniteVector(TargetPosition) &&
            IsFinite(TargetCollisionRadius) &&
            TargetCollisionRadius >= 0f &&
            IsFinite(MaximumTravelDistance) &&
            MaximumTravelDistance > 0f;

        public MeleeAdsorptionSimulationRequest(
            uint attackTick,
            Vector3 targetPosition,
            float targetCollisionRadius,
            float maximumTravelDistance)
        {
            AttackTick = attackTick;
            TargetPosition = targetPosition;
            TargetCollisionRadius = targetCollisionRadius;
            MaximumTravelDistance = maximumTravelDistance;
        }

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
        /// 检查数值是否可以安全进入角色模拟
        /// </summary>
        private static bool IsFinite(float value)
        {
            return
                !float.IsNaN(value) &&
                !float.IsInfinity(value);
        }
    }
}
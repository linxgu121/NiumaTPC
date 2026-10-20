using NiumaTPC.Core.Combat;
using NiumaTPC.Character.Simulation;
using UnityEngine;

namespace NiumaTPC.Character.Combat
{
    /// <summary>
    /// 一次近战吸附请求的角色侧数据
    /// 这也叫吸附意图
    /// 目标引用只用于本地短生命周期校验，不能直接发送到网络
    /// </summary>
    public readonly struct MeleeIntent
    {
        public IMeleeTarget Target { get; }

        public uint AttackTick { get; }

        public Vector3 SampledTargetPosition { get; }

        public float TargetCollisionRadius { get; }

        public float MaximumTravelDistance { get; }

        /// <summary>
        /// 检查目标状态和吸附快照是否仍可用于创建模拟请求
        /// </summary>
        public bool IsValid
        {
            get
            {
                if (IsMissingTarget(Target) ||
                    Target.RootTransform == null ||
                    !Target.IsTargetable)
                {
                    return false;
                }

                return IsFiniteVector(
                       SampledTargetPosition) &&
                       IsFinite(TargetCollisionRadius) &&
                       TargetCollisionRadius >= 0f &&
                       IsFinite(MaximumTravelDistance) &&
                       MaximumTravelDistance > 0f;
            }
        }

        public MeleeIntent(
            IMeleeTarget target,
            uint attackTick,
            Vector3 sampledTargetPosition,
            float targetCollisionRadius,
            float maximumTravelDistance)
        {
            Target = target;
            AttackTick = attackTick;
            SampledTargetPosition =
                sampledTargetPosition;
            TargetCollisionRadius =
                targetCollisionRadius;
            MaximumTravelDistance =
                maximumTravelDistance;
        }

        /// <summary>
        /// 重新检查本地目标并生成不含对象引用的固定 Tick 模拟请求
        /// 目标身份的网络转换由 FishNet 桥接层单独完成
        /// </summary>
        public bool TryCreateSimulationRequest(out MeleeAdsorptionSimulationRequest request)
        {
            request = default;

            if (!IsValid)
            {
                return false;
            }

            request = new MeleeAdsorptionSimulationRequest(
                AttackTick,
                SampledTargetPosition,
                TargetCollisionRadius,
                MaximumTravelDistance);

            return request.IsValid;
        }

        /// <summary>
        /// 检查目标接口是否为空或对应的 Unity 对象已经销毁
        /// </summary>
        private static bool IsMissingTarget(IMeleeTarget target)
        {
            if (target == null)
            {
                return true;
            }

            return target is Object unityObject && unityObject == null;
        }

        /// <summary>
        /// 检查三维向量是否全部为有限数值
        /// </summary>
        private static bool IsFiniteVector(
            Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        /// <summary>
        /// 检查数值是否可以安全进入固定 Tick 模拟
        /// </summary>
        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
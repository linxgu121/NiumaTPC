using NiumaTPC.Core.Combat;

namespace NiumaTPC.Character.Combat
{
    /// <summary>
    /// 已通过基础有效性与角度过滤的近战候选
    /// </summary>
    public readonly struct MeleeTargetCandidate
    {
        public IMeleeTarget Target { get; }

        public float HorizontalDistance { get; }

        public float Angle { get; }

        // 用于选择器内部排序，避免重新计算
        internal float SqrDistance { get; }

        // 只保证当前运行实例内稳定，不能作为网络目标身份
        internal int StableId { get; }

        internal MeleeTargetCandidate(
            IMeleeTarget target,
            float horizontalDistance,
            float angle,
            float sqrDistance,
            int stableId)
        {
            Target = target;
            HorizontalDistance = horizontalDistance;
            Angle = angle;
            SqrDistance = sqrDistance;
            StableId = stableId;
        }
    }
}
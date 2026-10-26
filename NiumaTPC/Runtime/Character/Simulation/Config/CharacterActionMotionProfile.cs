using System;

namespace NiumaTPC.Character.Simulation
{
    /// <summary>
    /// Roll 或 Dodge 已经按固定 Tick 烘焙完成的权威位移数据。
    /// 每个数组元素表示对应 Tick 应移动的距离，单位为米。
    /// </summary>
    public readonly struct CharacterActionMotionProfile
    {
        private readonly float[] _distanceSamples;

        /// <summary>
        /// 动作期间是否继续执行权威重力模拟。
        /// </summary>
        public readonly bool ApplyGravity;

        public bool IsValid =>
            _distanceSamples != null &&
            _distanceSamples.Length > 0;

        /// <summary>
        /// 动作持续的固定 Tick 数。
        /// ActionTick 的合法范围为 0 到 DurationTicks - 1。
        /// </summary>
        public uint DurationTicks =>
            IsValid ? (uint)_distanceSamples.Length : 0u;

        public CharacterActionMotionProfile(
            float[] distanceSamples,
            bool applyGravity)
        {
            if (distanceSamples == null || distanceSamples.Length == 0)
            {
                _distanceSamples = Array.Empty<float>();
                ApplyGravity = applyGravity;
                return;
            }

            // Profile 创建后不应再被外部修改。
            _distanceSamples = (float[])distanceSamples.Clone();

            for (int i = 0; i < _distanceSamples.Length; i++)
            {
                float distance = _distanceSamples[i];

                if (float.IsNaN(distance) ||
                    float.IsInfinity(distance) ||
                    distance < 0f)
                {
                    throw new ArgumentException(
                        $"动作位移采样[{i}]不是合法的非负有限值：{distance}。",
                        nameof(distanceSamples));
                }
            }

            ApplyGravity = applyGravity;
        }

        /// <summary>
        /// 获取当前 ActionTick 对应的位移距离。
        /// 返回 false 表示动作已经执行完毕。
        /// </summary>
        public bool TryGetDistance(
            uint actionTick,
            out float distance)
        {
            if (!IsValid ||
                actionTick >= (uint)_distanceSamples.Length)
            {
                distance = 0f;
                return false;
            }

            distance = _distanceSamples[(int)actionTick];
            return true;
        }
    }
}
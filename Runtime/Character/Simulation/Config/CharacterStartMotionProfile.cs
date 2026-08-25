using System;
using UnityEngine;

namespace NiumaTPC.Character.Simulation
{
    /// <summary>
    /// 一条已经按网络 Tick 预采样的起步运动配置。
    /// 它不持有动画播放器，也不读取 Time.time。
    /// </summary>
    public readonly struct CharacterStartMotionProfile
    {
        #region Sample Data(采样数据)

        private readonly float[] _speedSamples;
        private readonly float[] _rotationSamples;

        /// <summary>
        /// 曲线位移使用的角色局部方向。
        /// 当前旧资源大多为零，零值表示使用角色正前方。
        /// </summary>
        public readonly Vector3 LocalDirection;

        #endregion

        #region Properties(统一存放类的只读 / 读写封装属性)

        public bool IsValid => _speedSamples != null && _rotationSamples != null && _speedSamples.Length > 0 && _speedSamples.Length == _rotationSamples.Length;

        /// <summary>
        /// 起步曲线持续的网络 Tick 数。
        /// 数组包含 Tick 0，所以长度需要减一。
        /// </summary>
        public uint DurationTicks => IsValid ? (uint)(_speedSamples.Length - 1) : 0u;

        #endregion

        #region Constructor

        public CharacterStartMotionProfile(
            float[] speedSamples,
            float[] rotationSamples,
            Vector3 localDirection)
        {
            if (speedSamples == null || rotationSamples == null || speedSamples.Length == 0 || speedSamples.Length != rotationSamples.Length)
            {
                _speedSamples = Array.Empty<float>();
                _rotationSamples = Array.Empty<float>();
                LocalDirection = Vector3.zero;
                return;
            }

            _speedSamples = speedSamples;
            _rotationSamples = rotationSamples;

            localDirection.y = 0f;

            LocalDirection = localDirection.sqrMagnitude > 0.0001f ? localDirection.normalized : Vector3.zero;
        }

        #endregion

        #region Sampling

        public float GetSpeed(uint phaseTick)
        {
            if (!IsValid)
            {
                return 0f;
            }

            int index = GetClampedIndex(phaseTick);
            return _speedSamples[index];
        }

        /// <summary>
        /// 返回该 Tick 对应的累计旋转角度。
        /// 后续模拟器会使用当前值减去上一 Tick 的值，得到旋转增量。
        /// </summary>
        public float GetAccumulatedRotation(uint phaseTick)
        {
            if (!IsValid)
            {
                return 0f;
            }

            int index = GetClampedIndex(phaseTick);
            return _rotationSamples[index];
        }

        private int GetClampedIndex(uint phaseTick)
        {
            uint lastIndex = (uint)(_speedSamples.Length - 1);

            uint clampedTick = phaseTick > lastIndex ? lastIndex : phaseTick;

            return (int)clampedTick;
        }

        #endregion
    }
}
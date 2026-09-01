using System;

namespace NiumaTPC.Character.Simulation
{
    /// <summary>
    /// 已烘焙为固定 Tick 的翻越轨迹。
    /// 每个采样值表示对应 Tick 结束时的累计进度
    /// </summary>
    public readonly struct CharacterVaultMotionProfile
    {
        /// <summary>
        /// 第一阶段角色起跳向墙体靠近、身体抬升，手撑墙之前
        /// </summary>
        private readonly float[] _firstStageProgressSamples;
        /// <summary>
        /// 第二阶段翻过墙顶，向落地点下落
        /// </summary>
        private readonly float[] _secondStageProgressSamples;
        /// <summary>
        /// 翻越过程角色朝向旋转进度
        /// </summary>
        private readonly float[] _rotationProgressSamples;

        public bool IsValid =>
            _firstStageProgressSamples != null &&
            _secondStageProgressSamples != null &&
            _rotationProgressSamples != null &&
            _firstStageProgressSamples.Length > 0 &&
            _firstStageProgressSamples.Length ==
                _secondStageProgressSamples.Length &&
            _firstStageProgressSamples.Length ==
                _rotationProgressSamples.Length;

        public uint DurationTicks =>
            IsValid ? (uint)_firstStageProgressSamples.Length : 0u;

        public CharacterVaultMotionProfile(
            float[] firstStageProgressSamples,
            float[] secondStageProgressSamples,
            float[] rotationProgressSamples)
        {
            bool isInvalid =
                firstStageProgressSamples == null ||
                secondStageProgressSamples == null ||
                rotationProgressSamples == null ||
                firstStageProgressSamples.Length == 0 ||
                firstStageProgressSamples.Length !=
                    secondStageProgressSamples.Length ||
                firstStageProgressSamples.Length !=
                    rotationProgressSamples.Length;

            if (isInvalid)
            {
                _firstStageProgressSamples =
                    Array.Empty<float>();

                _secondStageProgressSamples =
                    Array.Empty<float>();

                _rotationProgressSamples =
                    Array.Empty<float>();

                return;
            }

            _firstStageProgressSamples =
                (float[])firstStageProgressSamples.Clone();

            _secondStageProgressSamples =
                (float[])secondStageProgressSamples.Clone();

            _rotationProgressSamples =
                (float[])rotationProgressSamples.Clone();

            for (int i = 0;
                 i < _firstStageProgressSamples.Length;
                 i++)
            {
                ValidateProgress(
                    _firstStageProgressSamples[i],
                    nameof(firstStageProgressSamples),
                    i);

                ValidateProgress(
                    _secondStageProgressSamples[i],
                    nameof(secondStageProgressSamples),
                    i);

                ValidateProgress(
                    _rotationProgressSamples[i],
                    nameof(rotationProgressSamples),
                    i);
            }
        }

        /// <summary>
        /// 获取当前 VaultTick 结束时的累计轨迹进度。
        /// </summary>
        public bool TryGetProgress(
            uint vaultTick,
            out float firstStageProgress,
            out float secondStageProgress,
            out float rotationProgress)
        {
            if (!IsValid ||
                vaultTick >=
                (uint)_firstStageProgressSamples.Length)
            {
                firstStageProgress = 0f;
                secondStageProgress = 0f;
                rotationProgress = 0f;
                return false;
            }

            int index = (int)vaultTick;

            firstStageProgress =
                _firstStageProgressSamples[index];

            secondStageProgress =
                _secondStageProgressSamples[index];

            rotationProgress =
                _rotationProgressSamples[index];

            return true;
        }

        private static void ValidateProgress(
            float value,
            string parameterName,
            int index)
        {
            bool isInvalid =
                float.IsNaN(value) ||
                float.IsInfinity(value) ||
                value < 0f ||
                value > 1f;

            if (isInvalid)
            {
                throw new ArgumentException(
                    $"翻越轨迹采样[{index}]必须位于0到1之间，当前值为{value}。",
                    parameterName);
            }
        }

    }
}
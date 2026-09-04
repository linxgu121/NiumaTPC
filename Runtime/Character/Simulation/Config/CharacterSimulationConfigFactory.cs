using System;
using NiumaTPC.Character.Config;
using NiumaTPC.Character.Config.PlayerSOModules;
using NiumaTPC.Character.Motion.MotionEnums;
using UnityEngine;

namespace NiumaTPC.Character.Simulation
{
    /// <summary>
    /// 静态工厂模式
    /// 把现有 PlayerSO 资源转换为角色模拟使用的纯数据配置。
    /// 模拟层不直接持有或读取 ScriptableObject。
    /// </summary>
    public static class CharacterSimulationConfigFactory
    {
        #region Public API

        public static CharacterSimulationConfig Create(PlayerSO playerConfig, float tickDeltaTime)
        {
            if (playerConfig == null)
            {
                throw new ArgumentNullException(nameof(playerConfig), "创建角色模拟配置时，PlayerSO 不能为空。");
            }

            CoreSO core = playerConfig.Core;

            if (core == null)
            {
                throw new InvalidOperationException($"PlayerSO“{playerConfig.name}”没有配置 CoreSO。");
            }

            AimingSO aiming = playerConfig.Aiming;

            if (aiming == null)
            {
                throw new InvalidOperationException($"PlayerSO“{playerConfig.name}”没有配置 AimingSO。");
            }

            ValidateAimingConfig(aiming, core);

            if (tickDeltaTime <= 0f || float.IsNaN(tickDeltaTime) || float.IsInfinity(tickDeltaTime))
            {
                throw new ArgumentOutOfRangeException(nameof(tickDeltaTime), tickDeltaTime, "Tick 时长必须是有限的正数");

            }

            LocomotionSO locomotion = playerConfig.LocomotionAnims;

            if (locomotion == null)
            {
                throw new InvalidOperationException(
                    $"PlayerSO“{playerConfig.name}”没有配置 LocomotionSO。");
            }

            JumpSO jump = playerConfig.JumpAndLanding;

            if (jump == null)
            {
                throw new InvalidOperationException(
                    $"PlayerSO“{playerConfig.name}”没有配置 JumpSO");
            }

            CharacterStartMotionProfile[] startProfiles =
                CreateStartProfiles(locomotion, tickDeltaTime);

            RollSO roll = playerConfig.Rolling;
            DodgingSO dodge = playerConfig.Dodging;
            VaultingSO vaulting = playerConfig.Vaulting;
            SlideSO slide = playerConfig.Sliding;

            /*
             * Roll、Dodge 属于高级可选模块。
             * 没有配置时生成无效 Profile，不影响基础角色模拟。
             */
            CharacterActionMotionProfile rollProfile =
                roll != null ? CreateActionMotionProfile(
                        roll.DistanceMeters,
                        roll.DurationSeconds,
                        roll.ProgressCurve,
                        roll.ApplyGravity,
                        tickDeltaTime,
                        roll.name)
                    : default;

            CharacterActionMotionProfile dodgeProfile =
                dodge != null ? CreateActionMotionProfile(
                        dodge.DistanceMeters,
                        dodge.DurationSeconds,
                        dodge.ProgressCurve,
                        dodge.ApplyGravity,
                        tickDeltaTime,
                        dodge.name)
                    : default;

            CharacterVaultMotionProfile lowVaultProfile =
                vaulting != null
                    ? CreateVaultMotionProfile(
                        vaulting.LowVaultDurationSeconds,
                        vaulting.LowVaultFirstStageEndNormalizedTime,
                        vaulting.LowVaultFirstStageProgressCurve,
                        vaulting.LowVaultSecondStageProgressCurve,
                        vaulting.LowVaultRotationProgressCurve,
                        tickDeltaTime,
                        $"{vaulting.name}/LowVault")
                    : default;

            CharacterVaultMotionProfile highVaultProfile =
                vaulting != null
                    ? CreateVaultMotionProfile(
                        vaulting.HighVaultDurationSeconds,
                        vaulting.HighVaultFirstStageEndNormalizedTime,
                        vaulting.HighVaultFirstStageProgressCurve,
                        vaulting.HighVaultSecondStageProgressCurve,
                        vaulting.HighVaultRotationProgressCurve,
                        tickDeltaTime,
                        $"{vaulting.name}/HighVault")
                    : default;

            CharacterSlideMotionProfile slideProfile =
                slide != null && slide.EnableSliding ? CreateSlideMotionProfile(
                    slide,
                    tickDeltaTime)
                : default;

            return new CharacterSimulationConfig(
                walkSpeed: core.WalkSpeed,
                jogSpeed: core.JogSpeed,
                sprintSpeed: core.SprintSpeed,
                moveSpeedSmoothTime: core.MoveSpeedSmoothTime,
                rotationSmoothTime: core.RotationSmoothTime,

                minimumViewPitch: core.PitchLimits.x,
                maximumViewPitch: core.PitchLimits.y,
                aimWalkSpeed: aiming.AimWalkSpeed,
                aimJogSpeed: aiming.AimJogSpeed,
                aimSprintSpeed: aiming.AimSprintSpeed,
                aimRotationSmoothTime: aiming.AimRotationSmoothTime,

                gravity: core.Gravity,
                groundedVerticalVelocity: core.ReboundForce,
                airControl: core.AirControl,

                defaultJumpInitialVelocity: jump.JumpForce,
                walkJumpInitialVelocity: jump.JumpForceWalk,
                sprintJumpInitialVelocity: jump.JumpForceSprint,
                sprintEmptyJumpInitialVelocity: jump.JumpForceSprintEmpty,

                doubleJumpInitialVelocity: jump.DoubleJumpForceUp,
                sprintEmptyDoubleJumpInitialVelocity: jump.DoubleJumpEmptyHandSprintForceUp,

                enableVaulting: vaulting != null && vaulting.EnableVaulting,
                lowVaultMinHeight: vaulting != null ? vaulting.LowVaultMinHeight : 0f,
                lowVaultMaxHeight: vaulting != null ? vaulting.LowVaultMaxHeight : 0f,
                highVaultMinHeight: vaulting != null ? vaulting.HighVaultMinHeight : 0f,
                highVaultMaxHeight: vaulting != null ? vaulting.HighVaultMaxHeight : 0f,

                startMotionProfiles: startProfiles,
                rollMotionProfile: rollProfile,
                dodgeMotionProfile: dodgeProfile,
                slideMotionProfile: slideProfile,

                lowVaultMotionProfile: lowVaultProfile,
                highVaultMotionProfile: highVaultProfile
            );
        }

        #endregion

        #region Aiming Validation(瞄准配置校验)

        private static void ValidateAimingConfig(
            AimingSO aiming,
            CoreSO core)
        {
            ValidateNonNegativeAimingValue(
                aiming.AimWalkSpeed,
                aiming.name,
                nameof(aiming.AimWalkSpeed));

            ValidateNonNegativeAimingValue(
                aiming.AimJogSpeed,
                aiming.name,
                nameof(aiming.AimJogSpeed));

            ValidateNonNegativeAimingValue(
                aiming.AimSprintSpeed,
                aiming.name,
                nameof(aiming.AimSprintSpeed));

            ValidateNonNegativeAimingValue(
                aiming.AimRotationSmoothTime,
                aiming.name,
                nameof(aiming.AimRotationSmoothTime));

            if (!IsFinite(core.PitchLimits.x) || !IsFinite(core.PitchLimits.y))
            {
                throw new InvalidOperationException($"“{core.name}.PitchLimits”必须包含有限数值。");
            }

            float minimumPitch = Mathf.Min(
                core.PitchLimits.x,
                core.PitchLimits.y);

            float maximumPitch = Mathf.Max(
                core.PitchLimits.x,
                core.PitchLimits.y);

            if (maximumPitch - minimumPitch < 0.001f)
            {
                throw new InvalidOperationException($"“{core.name}.PitchLimits”必须具有有效的俯仰范围。");
            }
        }

        private static void ValidateNonNegativeAimingValue(float value,string sourceName,string fieldName)
        {
            if (!IsFinite(value) || value < 0f)
            {
                throw new InvalidOperationException( $"“{sourceName}.{fieldName}”必须是非负有限值。");
            }
        }

        #endregion

        #region Start Motion Profiles(启动运动剖面集)

        private static CharacterStartMotionProfile[] CreateStartProfiles(LocomotionSO locomotion, float tickDeltaTime)
        {
            // 顺序必须和 CharacterStartDirection 的数值一致：
            // Forward、ForwardRight、Right、BackRight、
            // Back、BackLeft、Left、ForwardLeft
            MotionClipData[] sources =
            {
                //walk:索引0-7
                locomotion.WalkStartFwd,
                locomotion.WalkStartFwdRight,
                locomotion.WalkStartRight,
                locomotion.WalkStartBackRight,
                locomotion.WalkStartBack,
                locomotion.WalkStartBackLeft,
                locomotion.WalkStartLeft,
                locomotion.WalkStartFwdLeft,

                // Jog：索引 8～15
                locomotion.RunStartFwd,
                locomotion.RunStartFwdRight,
                locomotion.RunStartRight,
                locomotion.RunStartBackRight,
                locomotion.RunStartBack,
                locomotion.RunStartBackLeft,
                locomotion.RunStartLeft,
                locomotion.RunStartFwdLeft,

                // Sprint：索引 16～23
               locomotion.SprintStartFwd,
               locomotion.SprintStartFwdRight,
               locomotion.SprintStartRight,
               locomotion.SprintStartBackRight,
               locomotion.SprintStartBack,
               locomotion.SprintStartBackLeft,
               locomotion.SprintStartLeft,
               locomotion.SprintStartFwdLeft
            };

            var profile = new CharacterStartMotionProfile[sources.Length];

            for (int i = 0; i < sources.Length; i++)
            {
                profile[i] = CreateStartProfile(sources[i], tickDeltaTime);
            }

            return profile;
        }

        private static CharacterStartMotionProfile CreateStartProfile(MotionClipData source, float tickDeltaTime)
        {
            if (source == null || source.Type == MotionType.InputDriven ||
               source.SpeedCurve == null || source.SpeedCurve.length == 0)
            {
                return default;
            }

            float playbackSpeed =
                IsFinite(source.PlaybackSpeed) && source.PlaybackSpeed > 0.0001f
                    ? source.PlaybackSpeed
                    : 1f;

            float durationSeconds = GetProfileDurationSeconds(source, playbackSpeed);

            if (durationSeconds <= 0f)
            {
                return default;
            }

            uint durationTicks = (uint)Mathf.Max(
                1,
                Mathf.CeilToInt(durationSeconds / tickDeltaTime));

            int sampleCount = checked((int)durationTicks + 1);

            var speedSamples = new float[sampleCount];

            var rotationSamples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float stateTime = Mathf.Min(i * tickDeltaTime, durationSeconds);

                // 与旧 MotionDriver 保持一致：
                // AnimationCurve 使用经过 PlaybackSpeed 缩放的时间。
                float curveTime = stateTime * playbackSpeed;

                float speed = source.SpeedCurve.Evaluate(curveTime);

                float rotation =
                    source.RotationCurve != null &&
                    source.RotationCurve.length > 0
                        ? source.RotationCurve.Evaluate(curveTime)
                        : 0f;

                speedSamples[i] =
                    IsFinite(speed) ? Mathf.Max(0f, speed) : 0f;

                rotationSamples[i] = IsFinite(rotation) ? rotation : 0f;
            }

            return new CharacterStartMotionProfile(speedSamples, rotationSamples, source.TargetLocalDirection);

        }

        private static float GetProfileDurationSeconds(MotionClipData source, float playbackSpeed)
        {
            if (source.Type == MotionType.Mixed)
            {
                //旧 MotionDriver 在 RotationFinishedTime 后
                // 从曲线驱动切回输入驱动
                return Mathf.Max(0f, source.RotationFinishedTime);
            }

            float speedEnd = GetCurveEndTime(source.SpeedCurve);

            float rotationEnd = GetCurveEndTime(source.RotationCurve);

            float curveEnd = Mathf.Max(speedEnd, rotationEnd);

            return curveEnd / playbackSpeed;
        }

        private static float GetCurveEndTime(AnimationCurve curve)
        {
            if (curve == null || curve.length == 0)
            {
                return 0f;
            }

            Keyframe[] keys = curve.keys;

            return keys[keys.Length - 1].time;

        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        #endregion

        #region Action Motion Profiles(事件动作剖面集)

        /// <summary>
        /// 将总距离和累计进度曲线转换为逐 Tick 位移。
        /// </summary>
        private static CharacterActionMotionProfile
            CreateActionMotionProfile(
                float distanceMeters,
                float durationSeconds,
                AnimationCurve progressCurve,
                bool applyGravity,
                float tickDeltaTime,
                string sourceName)
        {
            if (!IsFinite(distanceMeters) || distanceMeters < 0f)
            {
                throw new InvalidOperationException(
                    $"“{sourceName}”的动作总距离必须是非负有限值。");
            }

            if (!IsFinite(durationSeconds) ||
                durationSeconds <= 0f)
            {
                throw new InvalidOperationException(
                    $"“{sourceName}”的动作持续时间必须是有限正数。");
            }

            ValidateActionProgressCurve(progressCurve, sourceName);

            int sampleCount = Mathf.Max(1, Mathf.CeilToInt(durationSeconds / tickDeltaTime));

            var distanceSamples = new float[sampleCount];

            float previousProgress = progressCurve.Evaluate(0f);

            for (int i = 0; i < sampleCount; i++)
            {
                /*
                 * 使用 Tick 结束边界采样累计进度。
                 * 最后一个 Tick 可能不足完整 tickDeltaTime，
                 * 但仍然会准确落在归一化时间 1。
                 */
                float normalizedTime = Mathf.Min(
                    (i + 1) * tickDeltaTime /
                    durationSeconds,
                    1f);

                float currentProgress = progressCurve.Evaluate(normalizedTime);

                float progressDelta = Mathf.Max(0f, currentProgress - previousProgress);

                distanceSamples[i] = distanceMeters * progressDelta;

                previousProgress = currentProgress;
            }

            return new CharacterActionMotionProfile(
                distanceSamples,
                applyGravity);
        }

        /// <summary>
        /// 验证累计位移曲线满足：
        /// 从 0 到 1、范围合法、单调不下降。
        /// </summary>
        private static void ValidateActionProgressCurve(
            AnimationCurve curve,
            string sourceName)
        {
            if (curve == null || curve.length < 2)
            {
                throw new InvalidOperationException(
                    $"“{sourceName}”没有配置有效的动作位移曲线。");
            }

            const float endpointTolerance = 0.001f;
            const float monotonicTolerance = 0.0001f;
            const int validationSteps = 128;

            float startProgress = curve.Evaluate(0f);
            float endProgress = curve.Evaluate(1f);

            if (!IsFinite(startProgress) ||
                !IsFinite(endProgress))
            {
                throw new InvalidOperationException(
                    $"“{sourceName}”的动作位移曲线包含无效数值。");
            }

            if (Mathf.Abs(startProgress) >
                endpointTolerance)
            {
                throw new InvalidOperationException(
                    $"“{sourceName}”的位移曲线必须从(0,0)开始。");
            }

            if (Mathf.Abs(endProgress - 1f) >
                endpointTolerance)
            {
                throw new InvalidOperationException(
                    $"“{sourceName}”的位移曲线必须在(1,1)结束。");
            }

            float previousProgress = startProgress;

            for (int i = 1; i <= validationSteps; i++)
            {
                float normalizedTime =
                    i / (float)validationSteps;

                float progress =
                    curve.Evaluate(normalizedTime);

                if (!IsFinite(progress))
                {
                    throw new InvalidOperationException(
                        $"“{sourceName}”的位移曲线包含无效数值。");
                }

                bool outsideRange =
                    progress < -endpointTolerance ||
                    progress > 1f + endpointTolerance;

                if (outsideRange)
                {
                    throw new InvalidOperationException(
                        $"“{sourceName}”的位移曲线必须保持在0到1之间。");
                }

                if (progress <
                    previousProgress - monotonicTolerance)
                {
                    throw new InvalidOperationException(
                        $"“{sourceName}”的位移曲线不能出现倒退。");
                }

                previousProgress = progress;
            }
        }
        #endregion

        #region Slide Motion Profile(滑铲运动剖面)

        private static CharacterSlideMotionProfile CreateSlideMotionProfile(
            SlideSO source,
            float tickDeltaTime)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            string sourceName = source.name;

            if (!IsFinite(source.MinimumStartSpeed) || source.MinimumStartSpeed <= 0f)
            {
                throw new InvalidOperationException($"“{sourceName}”的最低起滑速度必须是有限正数");
            }

            if (!IsFinite(source.StartSpeedMultiplier) || source.StartSpeedMultiplier <= 0f)
            {
                throw new InvalidOperationException($"“{sourceName}”的起滑速度倍率必须是有限正数");
            }

            if (!IsFinite(source.MaximumStartSpeed) || source.MaximumStartSpeed < source.MinimumStartSpeed)
            {
                throw new InvalidOperationException($"“{sourceName}”的起滑速度上限不能低于最低起滑速度");
            }

            if (!IsFinite(source.Deceleration) || source.Deceleration <= 0f)
            {
                throw new InvalidOperationException($"“{sourceName}”的滑铲减速度必须是有限正数");
            }

            if (!IsFinite(source.ExitSpeed) || source.ExitSpeed < 0f || source.ExitSpeed >= source.MinimumStartSpeed)
            {
                throw new InvalidOperationException($"“{sourceName}”的结束速度必须非负，并且低于最低起滑速度");
            }

            if (!IsFinite(source.MinimumDurationSeconds) || source.MinimumDurationSeconds < 0.1f || source.MinimumDurationSeconds > 0.15f)
            {
                throw new InvalidOperationException($"“{sourceName}”的最短持续时间必须位于0.1到0.15秒之间。");
            }

            if (!IsFinite(source.MaximumDurationSeconds) || source.MaximumDurationSeconds < source.MinimumDurationSeconds)
            {
                throw new InvalidOperationException($"“{sourceName}”的最长持续时间不能短于最短持续时间。");
            }

            uint minimumDurationTicks = (uint)Mathf.Max(
                1,
                Mathf.CeilToInt(source.MinimumDurationSeconds / tickDeltaTime));

            uint maximumDurationTicks = (uint)Mathf.Max(
                minimumDurationTicks,
                Mathf.CeilToInt(source.MaximumDurationSeconds / tickDeltaTime));

            float decelerationPerTick = source.Deceleration * tickDeltaTime;

            // 检查最低合法起滑速度是否能撑过最短动作时间。
            float minimumInitialSpeed = Mathf.Min(
                source.MaximumStartSpeed,
                source.MinimumStartSpeed * source.StartSpeedMultiplier);

            float speedAfterMinimumTicks = minimumInitialSpeed - decelerationPerTick * minimumDurationTicks;

            if (speedAfterMinimumTicks <= source.ExitSpeed)
            {
                throw new InvalidOperationException($"“{sourceName}”在最短持续时间结束前就会衰减到停止速度,请提高初速度、降低减速度或降低结束速度。");
            }

            return new CharacterSlideMotionProfile(
                source.MinimumStartSpeed,
                source.StartSpeedMultiplier,
                source.MaximumStartSpeed,
                decelerationPerTick,
                source.ExitSpeed,
                minimumDurationTicks,
                maximumDurationTicks,
                source.ApplyGravity);
        }

        #endregion

        #region Vault Motion Profiles(翻越动作剖面集)

        private static CharacterVaultMotionProfile
            CreateVaultMotionProfile(
                float durationSeconds,
                float firstStageEndNormalizedTime,
                AnimationCurve firstStageCurve,
                AnimationCurve secondStageCurve,
                AnimationCurve rotationCurve,
                float tickDeltaTime,
                string sourceName)
        {
            if (!IsFinite(durationSeconds) ||
                durationSeconds <= 0f)
            {
                throw new InvalidOperationException(
                    $"“{sourceName}”持续时间必须是有限正数。");
            }

            if (!IsFinite(firstStageEndNormalizedTime) ||
                firstStageEndNormalizedTime <= 0f ||
                firstStageEndNormalizedTime >= 1f)
            {
                throw new InvalidOperationException(
                    $"“{sourceName}”第一阶段结束时间必须位于0到1之间。");
            }

            ValidateActionProgressCurve(
                firstStageCurve,
                $"{sourceName}/FirstStage");

            ValidateActionProgressCurve(
                secondStageCurve,
                $"{sourceName}/SecondStage");

            ValidateActionProgressCurve(
                rotationCurve,
                $"{sourceName}/Rotation");

            int sampleCount = Mathf.Max(
                1,
                Mathf.CeilToInt(
                    durationSeconds / tickDeltaTime));

            var firstStageSamples =
                new float[sampleCount];

            var secondStageSamples =
                new float[sampleCount];

            var rotationSamples =
                new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float normalizedTime = Mathf.Min(
                    (i + 1) * tickDeltaTime /
                    durationSeconds,
                    1f);

                if (normalizedTime <=
                    firstStageEndNormalizedTime)
                {
                    float firstStageTime =
                        normalizedTime /
                        firstStageEndNormalizedTime;

                    firstStageSamples[i] =
                        firstStageCurve.Evaluate(
                            firstStageTime);

                    secondStageSamples[i] = 0f;
                }
                else
                {
                    firstStageSamples[i] = 1f;

                    float secondStageTime =
                        (normalizedTime -
                         firstStageEndNormalizedTime) /
                        (1f -
                         firstStageEndNormalizedTime);

                    secondStageSamples[i] =
                        secondStageCurve.Evaluate(
                            secondStageTime);
                }

                rotationSamples[i] =
                    rotationCurve.Evaluate(
                        normalizedTime);
            }

            return new CharacterVaultMotionProfile(
                firstStageSamples,
                secondStageSamples,
                rotationSamples);
        }

        #endregion

    }
}

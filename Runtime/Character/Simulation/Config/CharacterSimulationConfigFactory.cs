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
            if(playerConfig == null)
            {
                throw new ArgumentNullException(nameof(playerConfig), "创建角色模拟配置时，PlayerSO 不能为空。");
            }

            CoreSO core = playerConfig.Core;

            if(core == null)
            {
                throw new InvalidOperationException($"PlayerSO“{playerConfig.name}”没有配置 CoreSO。");
            }

            if(tickDeltaTime <= 0f || float.IsNaN(tickDeltaTime) || float.IsInfinity(tickDeltaTime))
            {
                throw new ArgumentOutOfRangeException(nameof(tickDeltaTime), tickDeltaTime,"Tick 时长必须是有限的正数");

            }

            LocomotionSO locomotion = playerConfig.LocomotionAnims;

            if (locomotion == null)
            {
                throw new InvalidOperationException(
                    $"PlayerSO“{playerConfig.name}”没有配置 LocomotionSO。");
            }

            JumpSO jump = playerConfig.JumpAndLanding;

            if(jump == null)
            {
                throw new InvalidOperationException(
                    $"PlayerSO“{playerConfig.name}”没有配置 JumpSO");
            }

            CharacterStartMotionProfile[] startProfiles =
                CreateStartProfiles(locomotion, tickDeltaTime);

            return new CharacterSimulationConfig(
                walkSpeed: core.WalkSpeed,
                jogSpeed: core.JogSpeed,
                sprintSpeed: core.SprintSpeed,
                moveSpeedSmoothTime: core.MoveSpeedSmoothTime,
                rotationSmoothTime: core.RotationSmoothTime,

                gravity: core.Gravity,
                groundedVerticalVelocity: core.ReboundForce,
                airControl: core.AirControl,
                jumpInitialVelocity:jump.JumpForce,
                
                startMotionProfiles: startProfiles
            );
        }

        #endregion

        #region Start Motion Profiles

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

            for(int i = 0; i < sources.Length; i++)
            {
                profile[i] = CreateStartProfile(sources[i], tickDeltaTime);
            }

            return profile;
        }

        private static CharacterStartMotionProfile CreateStartProfile(MotionClipData source, float tickDeltaTime)
        {
            if(source == null || source.Type == MotionType.InputDriven ||
               source.SpeedCurve == null || source.SpeedCurve.length == 0)
            {
                return default;
            }

            float playbackSpeed =
                IsFinite(source.PlaybackSpeed) && source.PlaybackSpeed > 0.0001f
                    ? source.PlaybackSpeed
                    : 1f;

            float durationSeconds = GetProfileDurationSeconds(source, playbackSpeed);

            if(durationSeconds <= 0f)
            {
                return default;
            }

            uint durationTicks = (uint)Mathf.Max(
                1,
                Mathf.CeilToInt(durationSeconds / tickDeltaTime));

            int sampleCount = checked((int)durationTicks + 1);

            var speedSamples = new float[sampleCount];

            var rotationSamples = new float[sampleCount];

            for(int i = 0; i < sampleCount; i++)
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
            if(source.Type == MotionType.Mixed)
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
            if(curve == null || curve.length == 0)
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
    }
}

using System;
using NiumaTPC.Character.Config;
using UnityEngine;

namespace NiumaTPC.Character.Arbitration.ArbitrationRequest
{
    /// <summary>
    /// 动作请求结构
    /// 动画片段 过度时间 动作优先级 是否启用重力
    /// </summary>
    [Serializable]
    public struct ActionRequest
    {
        public AnimationClip Clip;
        public MotionClipData MotionData;
        public float FadeDuration;
        public int Priority;
        public bool ApplyGravity;

        /// <summary>
        /// 构建动作请求
        /// </summary>
        public ActionRequest(AnimationClip clip, int priority = 20, float fadeDuration = 0.2f , bool applyGravity = true)
        {
            Clip = clip;
            MotionData = null;
            Priority = priority;
            FadeDuration = fadeDuration;
            ApplyGravity = applyGravity;
        }

        /// <summary>
        /// 构建可使用烘焙运动数据的动作请求
        /// </summary>
        public ActionRequest(MotionClipData motionData, int priority = 20, float fadeDuration = 0.2f , bool applyGravity = true)
        {
            MotionData = motionData;
            Clip = motionData != null && motionData.Clip != null ? motionData.Clip.Clip : null;
            Priority = priority;
            FadeDuration = fadeDuration;
            ApplyGravity = applyGravity;
        }
    }

}

using System;
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
        public float FadeDuration;
        public int Priority;
        public bool ApplyGravity;

        /// <summary>
        /// 构建动作请求
        /// </summary>
        public ActionRequest(AnimationClip clip, int priority = 20, float fadeDuration = 0.2f , bool applyGravity = true)
        {
            Clip = clip;
            Priority = priority;
            FadeDuration = fadeDuration;
            ApplyGravity = applyGravity;
        }
    }

}
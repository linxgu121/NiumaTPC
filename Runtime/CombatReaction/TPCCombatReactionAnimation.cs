using System;
using NiumaTPC.Character.Config;
using UnityEngine;

namespace NiumaTPC.CombatReaction
{
    [Serializable]
    public sealed class TPCCombatReactionAnimation
    {
        [InspectorName("表现类型")]
        [Tooltip("战斗表现类型。由 CombatBridge 根据 CombatResult 映射，例如轻受击、重受击、格挡、击倒、死亡。")]
        public TPCCombatReactionType ReactionType = TPCCombatReactionType.LightHit;

        [InspectorName("动画数据")]
        [Tooltip("必填。使用 TPC 原有 MotionClipData 配置动画，可被 RootMotionBaker 扫描和烘焙；运行时使用烘焙曲线驱动位移，不走 Animator RootMotion。")]
        public MotionClipData MotionData = new MotionClipData();

        [InspectorName("动作优先级")]
        [Tooltip("动作仲裁优先级。数值越大越容易打断当前 Override 动作。")]
        public int Priority = 60;

        [InspectorName("淡入时间")]
        [Tooltip("动画淡入时间。小于 0 会在 OnValidate 中修正为 0。")]
        public float FadeDuration = 0.08f;

        [InspectorName("冻结输入")]
        [Tooltip("播放期间是否冻结玩家输入。轻受击建议关闭，击倒和死亡可开启。")]
        public bool BlockInput;

        [InspectorName("应用重力")]
        [Tooltip("播放 Override 动作期间是否继续应用重力。")]
        public bool ApplyGravity = true;

        [InspectorName("立即仲裁")]
        [Tooltip("提交动作请求后是否本帧立即执行 ActionArbiter。通常开启。")]
        public bool FlushImmediately = true;

        public bool IsUsable => MotionData != null
            && MotionData.Clip != null
            && MotionData.Clip.Clip != null
            && ReactionType != TPCCombatReactionType.None;

        public void Normalize()
        {
            if (MotionData == null)
            {
                MotionData = new MotionClipData();
            }

            if (Priority < 0)
            {
                Priority = 0;
            }

            if (FadeDuration < 0f)
            {
                FadeDuration = 0f;
            }
        }
    }
}

using System;
using UnityEngine;

namespace NiumaTPC.CombatReaction
{
    [CreateAssetMenu(menuName = "NiumaTPC/战斗表现配置", fileName = "TPCCombatReactionProfile")]
    public sealed class TPCCombatReactionProfile : ScriptableObject
    {
        [Header("默认表现")]
        [InspectorName("默认受击表现")]
        [Tooltip("找不到指定类型表现时使用的默认受击动画。展开后填写“动画数据”；为空或未配置动画时，不播放兜底表现。")]
        public TPCCombatReactionAnimation DefaultHit = new TPCCombatReactionAnimation
        {
            ReactionType = TPCCombatReactionType.LightHit,
            Priority = 60,
            FadeDuration = 0.08f,
            BlockInput = false,
            ApplyGravity = true,
            FlushImmediately = true
        };

        [InspectorName("死亡表现")]
        [Tooltip("死亡表现。CombatResult.IsKilled=true 时优先使用。展开后填写“动画数据”，可参与 TPC RootMotionBaker 烘焙。")]
        public TPCCombatReactionAnimation Death = new TPCCombatReactionAnimation
        {
            ReactionType = TPCCombatReactionType.Death,
            Priority = 100,
            FadeDuration = 0.05f,
            BlockInput = true,
            ApplyGravity = true,
            FlushImmediately = true
        };

        [Header("按类型覆盖")]
        [InspectorName("按类型覆盖列表")]
        [Tooltip("按表现类型配置的表现表。轻受击、重受击、格挡、击倒等都放这里。死亡表现建议优先填上方“死亡表现”。")]
        public TPCCombatReactionAnimation[] Reactions = Array.Empty<TPCCombatReactionAnimation>();

        public bool TryGetAnimation(TPCCombatReactionType reactionType, out TPCCombatReactionAnimation animation)
        {
            animation = null;
            if (reactionType == TPCCombatReactionType.None)
            {
                return false;
            }

            if (reactionType == TPCCombatReactionType.Death && IsUsable(Death))
            {
                animation = Death;
                return true;
            }

            for (var i = 0; Reactions != null && i < Reactions.Length; i++)
            {
                var candidate = Reactions[i];
                if (candidate != null && candidate.ReactionType == reactionType && candidate.IsUsable)
                {
                    animation = candidate;
                    return true;
                }
            }

            if (reactionType != TPCCombatReactionType.Death && IsUsable(DefaultHit))
            {
                animation = DefaultHit;
                return true;
            }

            return false;
        }

        private void OnValidate()
        {
            if (DefaultHit == null)
            {
                DefaultHit = new TPCCombatReactionAnimation { ReactionType = TPCCombatReactionType.LightHit };
            }

            if (Death == null)
            {
                Death = new TPCCombatReactionAnimation { ReactionType = TPCCombatReactionType.Death, Priority = 100, BlockInput = true };
            }

            DefaultHit.Normalize();
            Death.ReactionType = TPCCombatReactionType.Death;
            Death.BlockInput = true;
            Death.Normalize();

            for (var i = 0; Reactions != null && i < Reactions.Length; i++)
            {
                Reactions[i]?.Normalize();
            }
        }

        private static bool IsUsable(TPCCombatReactionAnimation animation)
        {
            return animation != null && animation.IsUsable;
        }
    }
}

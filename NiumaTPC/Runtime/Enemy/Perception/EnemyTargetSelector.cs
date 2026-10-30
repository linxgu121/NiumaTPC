using System.Collections.Generic;
using NiumaTPC.Character.Combat;
using NiumaTPC.Core.Combat;
using NiumaTPC.Core.Detection;
using NiumaTPC.Core.Faction;
using UnityEngine;

namespace NiumaTPC.Enemy.Perception
{
    /// <summary>
    /// 从角色候选中选择距离、角度、阵营和视线均符合要求的目标
    /// 不负责维护感知碰撞体、目标记忆或AI状态
    /// </summary>
    public sealed class EnemyTargetSelector
    {
        #region Runtime State

        private readonly MeleeTargetSelector _targetRanker = new MeleeTargetSelector();

        private readonly MeleeTargetLineOfSightChecker _lineOfSightChecker = new MeleeTargetLineOfSightChecker();

        private readonly List<MeleeTargetCandidate> _candidateBuffer = new List<MeleeTargetCandidate>();

        #endregion

        #region Public API

        /// <summary>
        /// 从候选集合中选择排序最优且可见的敌对目标
        /// 未分配队伍、同队、超出距离或被障碍阻挡的目标不会通过
        /// </summary>
        public bool TrySelectTarget(
            ICandidateProvider<IMeleeTarget> provider,
            Transform observerRoot,
            ITeamAffiliation observerAffiliation,
            Vector3 origin,
            Vector3 forward,
            float maximumAngleFromForward,
            float maximumDistance,
            PhysicsScene physicsScene,
            LayerMask lineOfSightMask,
            float lineOfSightRadius,
            QueryTriggerInteraction triggerInteraction,
            out MeleeTargetCandidate selectedCandidate)
        {
            selectedCandidate = default;

            if (provider == null ||
                observerRoot == null ||
                !TeamRelationshipUtility.IsAssigned(observerAffiliation) ||
                !IsPositiveFinite(maximumDistance))
            {
                return false;
            }

            int candidateCount = _targetRanker.CopyRankedCandidates(
                provider,
                observerRoot,
                origin,
                forward,
                maximumAngleFromForward,
                _candidateBuffer);

            for (int i = 0; i < candidateCount; i++)
            {
                MeleeTargetCandidate candidate = _candidateBuffer[i];

                if (candidate.HorizontalDistance > maximumDistance)
                {
                    continue;
                }

                if (!TryResolveAffiliation(candidate.Target, out ITeamAffiliation targetAffiliation))
                {
                    continue;
                }

                if (!TeamRelationshipUtility.IsHostile(observerAffiliation, targetAffiliation))
                {
                    continue;
                }

                if (!_lineOfSightChecker.HasLineOfSight(
                        physicsScene,
                        observerRoot,
                        origin,
                        candidate.Target,
                        lineOfSightMask,
                        lineOfSightRadius,
                        triggerInteraction,
                        out _))
                {
                    continue;
                }

                selectedCandidate = candidate;
                return true;
            }

            return false;
        }

        #endregion

        #region Target Affiliation

        /// <summary>
        /// 从整名角色目标的根节点解析已经分配的队伍归属
        /// 没有TeamAffiliation或TeamId为0的目标不能参与敌我判断
        /// </summary>
        private static bool TryResolveAffiliation(IMeleeTarget target, out ITeamAffiliation affiliation)
        {
            affiliation = null;
            if (target == null || target.RootTransform == null)
            {
                return false;
            }

            affiliation = target.RootTransform.GetComponentInParent<ITeamAffiliation>();
            return TeamRelationshipUtility.IsAssigned(affiliation);
        }

        #endregion

        #region Validation

        /// <summary>
        /// 检查距离配置是否为大于零的有限值
        /// </summary>
        private static bool IsPositiveFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;
        }

        #endregion
    }
}

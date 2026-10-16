using System;
using System.Collections.Generic;
using NiumaTPC.Core.Combat;
using NiumaTPC.Core.Detection;
using UnityEngine;

namespace NiumaTPC.Character.Combat
{
    /// <summary>
    /// 根据目标有效性、前方夹角和水平距离排列近战吸附目标
    /// 不负责障碍检测、角色位移、攻击执行或伤害结算
    /// </summary>
    public sealed class MeleeTargetSelector
    {
        private const float MinimumPlanarSqrMagnitude = 0.000001f;

        #region Runtime State

        // 接收候选提供者的数据，避免每次选择时创建列表
        private readonly List<IMeleeTarget> _candidateBuffer = new();

        // 为单目标兼容入口保存排序结果
        private readonly List<MeleeTargetCandidate> _rankedBuffer = new();

        #endregion

        #region Public API

        /// <summary>
        /// 从候选集合中选择排序后的第一名目标
        /// 保留该入口供暂时不需要障碍降级的调用方使用
        /// </summary>
        public bool TrySelectNearest(
            ICandidateProvider<IMeleeTarget> provider,
            Transform attackerRoot,
            Vector3 origin,
            Vector3 forward,
            float maxAngleFromForward,
            out IMeleeTarget selectedTarget,
            out float selectedDistance,
            out float selectedAngle)
        {
            selectedTarget = null;
            selectedDistance = 0f;
            selectedAngle = 0f;

            int candidateCount = CopyRankedCandidates(
                provider,
                attackerRoot,
                origin,
                forward,
                maxAngleFromForward,
                _rankedBuffer);

            if (candidateCount <= 0)
            {
                return false;
            }

            MeleeTargetCandidate bestCandidate = _rankedBuffer[0];

            selectedTarget = bestCandidate.Target;
            selectedDistance = bestCandidate.HorizontalDistance;
            selectedAngle = bestCandidate.Angle;

            return true;
        }

        /// <summary>
        /// 复制通过基础过滤的候选并按距离、夹角和本地稳定编号排序
        /// 不检查场景障碍物
        /// </summary>
        public int CopyRankedCandidates(
            ICandidateProvider<IMeleeTarget> provider,
            Transform attackerRoot,
            Vector3 origin,
            Vector3 forward,
            float maxAngleFromForward,
            List<MeleeTargetCandidate> destination)
        {
            if (destination == null)
            {
                throw new ArgumentNullException(nameof(destination));
            }

            destination.Clear();

            if (provider == null ||
                attackerRoot == null ||
                provider is UnityEngine.Object providerObject &&
                providerObject == null)
            {
                return 0;
            }

            Vector3 planarForward = forward;
            planarForward.y = 0f;

            if (planarForward.sqrMagnitude <= MinimumPlanarSqrMagnitude)
            {
                return 0;
            }

            planarForward.Normalize();

            float clampedAngle = Mathf.Clamp(
                maxAngleFromForward,
                0f,
                180f);

            float minimumDot = Mathf.Cos(clampedAngle * Mathf.Deg2Rad);

            provider.CopyCandidates(_candidateBuffer);

            for (int i = 0;
                 i < _candidateBuffer.Count;
                 i++)
            {
                IMeleeTarget candidate = _candidateBuffer[i];

                if (IsMissingTarget(candidate) || !candidate.IsTargetable)
                {
                    continue;
                }

                Transform candidateRoot = candidate.RootTransform;

                if (candidateRoot == null ||
                    IsSameCharacter(
                        attackerRoot,
                        candidateRoot))
                {
                    continue;
                }

                Vector3 toCandidate = candidate.MagnetismPosition - origin;

                toCandidate.y = 0f;

                float sqrDistance = toCandidate.sqrMagnitude;

                float directionDot;

                if (sqrDistance <= MinimumPlanarSqrMagnitude)
                {
                    // 水平位置重合时视为正前方
                    directionDot = 1f;
                }
                else
                {
                    directionDot = Vector3.Dot(
                        planarForward,
                        toCandidate /
                        Mathf.Sqrt(sqrDistance));
                }

                if (directionDot < minimumDot)
                {
                    continue;
                }

                float horizontalDistance = Mathf.Sqrt(sqrDistance);

                float angle = Mathf.Acos(
                    Mathf.Clamp(
                        directionDot,
                        -1f,
                        1f)) * Mathf.Rad2Deg;

                destination.Add(
                    new MeleeTargetCandidate(
                        candidate,
                        horizontalDistance,
                        angle,
                        sqrDistance,
                        candidateRoot.GetInstanceID()));
            }

            destination.Sort(CompareCandidates);

            return destination.Count;
        }

        #endregion

        #region Candidate Evaluation

        /// <summary>
        /// 检查接口引用是否为空或对应的 Unity 对象已经销毁
        /// </summary>
        private static bool IsMissingTarget(
            IMeleeTarget target)
        {
            if (target == null)
            {
                return true;
            }

            return target is UnityEngine.Object unityObject && unityObject == null;
        }

        /// <summary>
        /// 检查候选根节点是否与攻击者属于同一角色层级
        /// </summary>
        private static bool IsSameCharacter(
            Transform attackerRoot,
            Transform candidateRoot)
        {
            return candidateRoot == attackerRoot ||
                   candidateRoot.IsChildOf(attackerRoot) ||
                   attackerRoot.IsChildOf(candidateRoot);
        }

        /// <summary>
        /// 按水平距离、中心夹角和本地稳定编号排列候选
        /// </summary>
        private static int CompareCandidates(
            MeleeTargetCandidate left,
            MeleeTargetCandidate right)
        {
            int distanceComparison =
                left.SqrDistance.CompareTo(
                    right.SqrDistance);

            if (distanceComparison != 0)
            {
                return distanceComparison;
            }

            int angleComparison = left.Angle.CompareTo(right.Angle);

            if (angleComparison != 0)
            {
                return angleComparison;
            }

            return left.StableId.CompareTo(right.StableId);
        }

        #endregion
    }
}
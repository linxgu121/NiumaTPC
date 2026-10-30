using System;
using System.Collections.Generic;
using NiumaTPC.Character;
using NiumaTPC.Character.Combat;
using NiumaTPC.Character.Simulation;
using NiumaTPC.Core.Combat;
using NiumaTPC.Core.Faction;
using UnityEngine;

namespace NiumaTPC.FishNet.Combat
{
    /// <summary>
    /// 在服务器权威 Tick 中执行近战攻击盒查询
    /// 负责攻击窗口、目标去重和命中部位选择，不负责伤害结算
    /// </summary>
    [Serializable]
    public sealed class ServerMeleeHitProcessor
    {
        #region Inspector(面板配置)

        [SerializeField]
        [Tooltip("允许近战攻击盒查询的受击区域 Layer，必须包含 DamageHurtbox")]
        private LayerMask hurtboxLayers;

        [SerializeField]
        [Tooltip("开发环境中输出服务器权威命中日志")]
        private bool logHits = true;

        #endregion

        #region Runtime State(运行时状态)

        private readonly MeleeHitCollision _collision = new MeleeHitCollision();

        private readonly List<DamageHurtbox> _candidates = new List<DamageHurtbox>(32);

        private readonly Dictionary<Component, SelectedHit> _bestHitsByTarget = new Dictionary<Component, SelectedHit>(16);

        private readonly HashSet<Component> _hitTargets = new HashSet<Component>();

        private bool _hasTrackedAttack;
        private uint _trackedAttackSequence;

        private bool _warnedInvalidConfiguration;
        private bool _warnedInvalidQuery;
        private bool _warnedOverflow;

        #endregion

        #region Internal Types(内部类型)

        private readonly struct SelectedHit
        {
            public readonly DamageHurtbox Hurtbox;
            public readonly float SqrDistance;

            public SelectedHit(
                DamageHurtbox hurtbox,
                float sqrDistance)
            {
                Hurtbox = hurtbox;
                SqrDistance = sqrDistance;
            }
        }

        #endregion

        #region Public API(公共接口)

        /// <summary>
        /// 清理服务器近战命中处理状态
        /// 应在服务器开始和停止时调用
        /// </summary>
        public void Reset()
        {
            _hasTrackedAttack = false;
            _trackedAttackSequence = 0u;

            _warnedInvalidConfiguration = false;
            _warnedInvalidQuery = false;
            _warnedOverflow = false;

            _candidates.Clear();
            _bestHitsByTarget.Clear();
            _hitTargets.Clear();
        }

        /// <summary>
        /// 检查当前服务器模拟状态是否进入攻击有效期并查询权威命中
        /// 同一次攻击对同一个DamageableComponent只提交一次命中日志
        /// </summary>
        public void Process(
            NiumaCharacterController attacker,
            IMeleeAttackSource attackSource,
            in CharacterSimulationState state,
            float tickDeltaTime,
            int attackerObjectId,
            UnityEngine.Object logContext)
        {
            if (attacker == null ||
                attacker.RuntimeData == null ||
                attacker.RuntimeData.IsDead ||
                attacker.RuntimeData.Arbitration.IsDead ||
                attackSource == null ||
                state.MeleeAttackType == MeleeAttackType.None ||
                state.MeleeAttackSourceRevision == 0u)
            {
                return;
            }

            ITeamAffiliation attackerAffiliation = attacker.GetComponentInParent<ITeamAffiliation>();
            if (!TeamRelationshipUtility.IsAssigned(attackerAffiliation)) return;

            TrackAttackSequence(state.MeleeAttackSequence);

            if (!TryResolveAttackStep(attackSource, in state, tickDeltaTime, out MeleeComboStep step))
            {
                return;
            }

            if (!step.TryCreateHitWindow(tickDeltaTime, out uint hitWindowStartTicks, out uint hitWindowEndTicks))
            {
                WarnInvalidConfiguration(attackerObjectId, state.MeleeAttackSequence, logContext);
                return;
            }

            uint attackAgeTicks = unchecked(state.Tick - state.MeleeAttackStartTick);

            if (attackAgeTicks < hitWindowStartTicks || attackAgeTicks >= hitWindowEndTicks)
            {
                return;
            }

            PhysicsScene physicsScene = attacker.gameObject.scene.GetPhysicsScene();

            MeleeHitCollisionResult queryResult = _collision.QueryBox(
                physicsScene,
                state.Position,
                state.Yaw,
                step.HitBoxLocalCenter,
                step.HitBoxHalfExtents,
                hurtboxLayers,
                _candidates,
                out Vector3 worldCenter);

            if (queryResult == MeleeHitCollisionResult.Invalid)
            {
                WarnInvalidQuery(attackerObjectId, state.MeleeAttackSequence, logContext);
                return;
            }

            if (queryResult == MeleeHitCollisionResult.Overflow)
            {
                WarnOverflow(attackerObjectId, state.MeleeAttackSequence, logContext);
                return;
            }

            if (queryResult != MeleeHitCollisionResult.Hit)
            {
                return;
            }

            SelectBestHitPerTarget(attacker, attackerAffiliation, worldCenter);
            SubmitNewHits(in state, step, attackerObjectId, logContext);
        }

        #endregion

        #region Attack Resolution(攻击解析)

        /// <summary>
        /// 检查服务器攻击来源、来源修订号、攻击类型与当前连招段是否对应
        /// 防止攻击成立后切换武器或更换怪物攻击配置仍继续产生命中
        /// </summary>
        private static bool TryResolveAttackStep(
            IMeleeAttackSource attackSource,
            in CharacterSimulationState state,
            float tickDeltaTime,
            out MeleeComboStep step)
        {
            step = null;

            if (!attackSource.TryGetAttackDefinition(
                    state.MeleeAttackType,
                    tickDeltaTime,
                    out MeleeAttackDefinition definition) ||
                !definition.IsValid ||
                definition.AttackType != state.MeleeAttackType ||
                definition.SourceRevision != state.MeleeAttackSourceRevision)
            {
                return false;
            }

            return definition.Combo.TryGetStep(state.MeleeComboStepIndex, out step);
        }

        /// <summary>
        /// 切换攻击序号时清理上一轮攻击的目标去重和诊断状态
        /// 同一攻击序号的连续有效帧不会重复清理
        /// </summary>
        private void TrackAttackSequence(uint attackSequence)
        {
            if (_hasTrackedAttack && _trackedAttackSequence == attackSequence) return;

            _hasTrackedAttack = true;
            _trackedAttackSequence = attackSequence;

            _warnedInvalidConfiguration = false;
            _warnedInvalidQuery = false;
            _warnedOverflow = false;

            _bestHitsByTarget.Clear();
            _hitTargets.Clear();
        }

        #endregion

        #region Hit Selection(命中选择)

        private void SelectBestHitPerTarget(
            NiumaCharacterController attacker,
            ITeamAffiliation attackerAffiliation,
            Vector3 worldCenter)
        {
            _bestHitsByTarget.Clear();

            for (int i = 0; i < _candidates.Count; i++)
            {
                DamageHurtbox hurtbox = _candidates[i];

                if (hurtbox == null || !hurtbox.TryGetDamageable(out _) || hurtbox.HurtboxCollider == null)
                {
                    continue;
                }

                Component damageableComponent = hurtbox.DamageableComponent;

                if (damageableComponent == null || ReferenceEquals(damageableComponent, attacker))
                {
                    continue;
                }

                ITeamAffiliation targetAffiliation = damageableComponent.GetComponentInParent<ITeamAffiliation>();

                // 服务器只允许当前基础规则认定的敌对队伍进入权威命中结果
                if (!TeamRelationshipUtility.IsHostile(attackerAffiliation, targetAffiliation))
                {
                    continue;
                }

                Vector3 closestPoint = hurtbox.HurtboxCollider.ClosestPoint(worldCenter);
                float sqrDistance = (closestPoint - worldCenter).sqrMagnitude;
                SelectedHit candidate = new SelectedHit(hurtbox, sqrDistance);

                if (_bestHitsByTarget.TryGetValue(damageableComponent, out SelectedHit current) &&
                    !IsBetterHit(in candidate, in current))
                {
                    continue;
                }

                _bestHitsByTarget[damageableComponent] = candidate;
            }
        }

        /// <summary>
        /// 比较同一角色的两个受击区域并选择更接近攻击盒中心的部位
        /// 距离近似相同时使用 HitSpot 枚举值保持选择稳定
        /// </summary>
        private static bool IsBetterHit(in SelectedHit candidate, in SelectedHit current)
        {
            const float distanceTolerance = 0.0001f;

            if (candidate.SqrDistance + distanceTolerance < current.SqrDistance)
            {
                return true;
            }

            if (Mathf.Abs(candidate.SqrDistance - current.SqrDistance) > distanceTolerance)
            {
                return false;
            }

            return (byte)candidate.Hurtbox.HitSpot < (byte)current.Hurtbox.HitSpot;
        }

        private void SubmitNewHits(
            in CharacterSimulationState state,
            MeleeComboStep step,
            int attackerObjectId,
            UnityEngine.Object logContext)
        {
            foreach (KeyValuePair<Component, SelectedHit> pair in _bestHitsByTarget)
            {
                Component target = pair.Key;
                SelectedHit selectedHit = pair.Value;

                if (!_hitTargets.Add(target) ||
                    !selectedHit.Hurtbox.TryGetDamageable(out IDamageable damageable))
                {
                    continue;
                }

                var request = new DamageRequest(
                    step.Damage,
                    selectedHit.Hurtbox.HitSpot,
                    attackerObjectId,
                    state.MeleeAttackSequence,
                    state.MeleeAttackSourceRevision);

                damageable.RequestDamage(in request);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (logHits)
                {
                    Debug.Log(
                        $"[近战权威命中] " +
                        $"AttackerObjectId={attackerObjectId}, " +
                        $"Sequence={state.MeleeAttackSequence}, " +
                        $"SourceRevision={state.MeleeAttackSourceRevision}, " +
                        $"AttackType={state.MeleeAttackType}, " +
                        $"Step={state.MeleeComboStepIndex}, " +
                        $"Target={target.name}, " +
                        $"HitSpot={selectedHit.Hurtbox.HitSpot}, " +
                        $"BaseDamage={step.Damage:F2}, " +
                        $"Tick={state.Tick}",
                        logContext);
                }
#endif
            }
        }

        #endregion

        #region Diagnostics(诊断)

        private void WarnInvalidConfiguration(
            int attackerObjectId,
            uint attackSequence,
            UnityEngine.Object logContext)
        {
            if (_warnedInvalidConfiguration)
            {
                return;
            }

            _warnedInvalidConfiguration = true;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning(
                $"[近战权威判定] 攻击窗口或攻击盒配置无效，" +
                $"ObjectId={attackerObjectId}, " +
                $"Sequence={attackSequence}",
                logContext);
#endif
        }

        private void WarnInvalidQuery(
            int attackerObjectId,
            uint attackSequence,
            UnityEngine.Object logContext)
        {
            if (_warnedInvalidQuery)
            {
                return;
            }

            _warnedInvalidQuery = true;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning(
                $"[近战权威判定] 物理查询参数无效，" +
                $"请检查 Hurtbox Layers，" +
                $"ObjectId={attackerObjectId}, " +
                $"Sequence={attackSequence}",
                logContext);
#endif
        }

        private void WarnOverflow(
            int attackerObjectId,
            uint attackSequence,
            UnityEngine.Object logContext)
        {
            if (_warnedOverflow)
            {
                return;
            }

            _warnedOverflow = true;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning(
                $"[近战权威判定] 攻击盒候选数量超过缓冲容量，" +
                $"本次不使用不完整结果，" +
                $"ObjectId={attackerObjectId}, " +
                $"Sequence={attackSequence}",
                logContext);
#endif
        }

        #endregion
    }
}
using System;
using System.Collections.Generic;
using NiumaTPC.Core.Combat;
using NiumaTPC.Core.Detection;
using UnityEngine;

namespace NiumaTPC.Character.Combat
{
    /// <summary>
    /// 维护近战感知范围内的整名角色目标
    /// 不负责评分、吸附、攻击动画或伤害结算
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SphereCollider))]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class MeleeTargetSensor : MonoBehaviour,ICandidateProvider<IMeleeTarget>
    {
        #region Inspector

        [SerializeField]
        [Min(0.1f)]
        [Tooltip("候选感知半径，使用当前物体的本地单位，建议该物体保持Scale为1")]
        private float detectionRadius = 3f;

        [SerializeField]
        [Tooltip("允许进入候选集合的角色碰撞层，不要包含近战感知器自身的Layer")]
        private LayerMask targetLayers = ~0;

        #endregion

        #region Runtime State

        private SphereCollider _sensorCollider;
        private Rigidbody _sensorRigidbody;
        private MeleeCharacterTarget _ownerTarget;

        // 一个角色可能有多个Collider，计数可避免其中一个退出就提前移除目标
        private readonly Dictionary<MeleeCharacterTarget, int> _overlapCounts = new();

        private readonly List<MeleeCharacterTarget> _staleTargets = new();

        #endregion

        #region Public API

        public float DetectionRadius => detectionRadius;

        public int CandidateCount
        {
            get
            {
                RemoveDestroyedTargets();
                return _overlapCounts.Count;
            }
        }

        /// <summary>
        /// 将当前范围内的整名角色目标复制到调用方缓冲区
        /// 本方法不负责过滤死亡、角度或障碍物
        /// </summary>
        public int CopyCandidates(
            List<IMeleeTarget> destination)
        {
            if (destination == null)
            {
                throw new ArgumentNullException(
                    nameof(destination));
            }

            destination.Clear();
            RemoveDestroyedTargets();

            foreach (MeleeCharacterTarget target
                     in _overlapCounts.Keys)
            {
                destination.Add(target);
            }

            return destination.Count;
        }

        #endregion

        #region Unity Lifecycle

        private void Reset()
        {
            CacheAndConfigureComponents();
        }

        private void Awake()
        {
            _ownerTarget =
                GetComponentInParent<MeleeCharacterTarget>();

            if (_ownerTarget == null)
            {
                Debug.LogError(
                    "[近战感知器] 父级中没有找到 MeleeCharacterTarget",
                    this);

                enabled = false;
                return;
            }

            CacheAndConfigureComponents();
        }

        private void OnValidate()
        {
            detectionRadius =
                Mathf.Max(0.1f, detectionRadius);

            CacheAndConfigureComponents();
        }

        private void OnDisable()
        {
            _overlapCounts.Clear();
            _staleTargets.Clear();
        }

        private void OnTriggerEnter(Collider other)
        {
            MeleeCharacterTarget target =
                ResolveTarget(other);

            if (target == null)
            {
                return;
            }

            if (_overlapCounts.TryGetValue(
                    target,
                    out int overlapCount))
            {
                _overlapCounts[target] =
                    overlapCount + 1;
            }
            else
            {
                _overlapCounts.Add(target, 1);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            MeleeCharacterTarget target = ResolveTargetWithoutLayerCheck(other);

            if (target == null ||
                !_overlapCounts.TryGetValue(
                    target,
                    out int overlapCount))
            {
                return;
            }

            overlapCount--;

            if (overlapCount <= 0)
            {
                _overlapCounts.Remove(target);
            }
            else
            {
                _overlapCounts[target] = overlapCount;
            }
        }

        #endregion

        #region Candidate Resolution

        /// <summary>
        /// 检查碰撞体层级并将子碰撞体归一化为整名角色目标
        /// 自己和其他感知范围碰撞体不会成为候选
        /// </summary>
        private MeleeCharacterTarget ResolveTarget(
            Collider other)
        {
            if (other == null || !ContainsTargetLayer(other.gameObject.layer))
            {
                return null;
            }

            // 防止两个角色的感知球互相被识别成角色本体
            if (other.TryGetComponent(
                    out MeleeTargetSensor _))
            {
                return null;
            }

            return ResolveTargetWithoutLayerCheck(other);
        }

        private MeleeCharacterTarget ResolveTargetWithoutLayerCheck(Collider other)
        {
            if (other == null)
            {
                return null;
            }

            MeleeCharacterTarget target =other.GetComponentInParent<MeleeCharacterTarget>();

            if (target == null ||
                target == _ownerTarget)
            {
                return null;
            }

            return target;
        }

        private bool ContainsTargetLayer(int layer)
        {
            return (targetLayers.value &(1 << layer)) != 0;
        }

        private void RemoveDestroyedTargets()
        {
            _staleTargets.Clear();

            foreach (MeleeCharacterTarget target in _overlapCounts.Keys)
            {
                if (target == null)
                {
                    _staleTargets.Add(target);
                }
            }

            for (int i = 0; i < _staleTargets.Count; i++)
            {
                _overlapCounts.Remove(
                    _staleTargets[i]);
            }
        }

        #endregion

        #region Component Configuration

        private void CacheAndConfigureComponents()
        {
            if (_sensorCollider == null)
            {
                _sensorCollider = GetComponent<SphereCollider>();
            }

            if (_sensorRigidbody == null)
            {
                _sensorRigidbody = GetComponent<Rigidbody>();
            }

            if (_sensorCollider != null)
            {
                _sensorCollider.isTrigger = true;
                _sensorCollider.radius = detectionRadius;
            }

            if (_sensorRigidbody != null)
            {
                // Rigidbody仅用于保证Trigger事件稳定触发
                // 实际角色位移仍由CharacterController负责
                _sensorRigidbody.isKinematic = true;
                _sensorRigidbody.useGravity = false;
                _sensorRigidbody.detectCollisions = true;
            }
        }

        #endregion
    }
}
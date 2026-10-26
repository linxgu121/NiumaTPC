using System.Collections.Generic;
using NiumaTPC.Core.Combat;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NiumaTPC.Character.Combat
{
    /// <summary>
    /// 保存角色侧近战选敌配置并提供统一选敌入口
    /// 不负责输入、攻击动画、吸附位移或伤害结算
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NiumaCharacterController))]
    public sealed class MeleeTargetingController : MonoBehaviour
    {
        #region Inspector

        [Header("候选选择")]

        [SerializeField]
        [Tooltip("候选感知组件，通常绑定角色子物体上的 MeleeTargetSensor，未绑定时自动从子物体查找")]
        private MeleeTargetSensor targetSensor;

        [SerializeField]
        [Range(0f, 180f)]
        [Tooltip("角色前方向单侧最大选敌角度，60代表左右各60度")]
        private float maxAngleFromForward = 60f;

        [Header("障碍检测")]

        [SerializeField]
        [Tooltip("视线查询层，必须同时包含场景障碍层和角色实体碰撞层")]
        private LayerMask lineOfSightLayers = ~0;

        [SerializeField]
        [Min(0f)]
        [Tooltip("视线球形扫掠半径，0使用Raycast，大于0使用SphereCast")]
        private float lineOfSightRadius = 0.1f;

        [SerializeField]
        [Tooltip("视线查询是否命中Trigger，通常使用Ignore避免命中近战感知球")]
        private QueryTriggerInteraction lineOfSightTriggerInteraction =
            QueryTriggerInteraction.Ignore;

        #endregion

        #region Runtime State

        private NiumaCharacterController _character;

        private readonly MeleeTargetSelector _selector =
            new();

        private readonly MeleeTargetLineOfSightChecker
            _lineOfSightChecker = new();

        private readonly List<MeleeTargetCandidate>
            _rankedCandidates = new();

        #endregion

        #region Public API

        public float MaxAngleFromForward => maxAngleFromForward;

        public MeleeTargetSensor TargetSensor => targetSensor;

        /// <summary>
        /// 按优先级检查当前候选并返回第一名无遮挡目标
        /// 被障碍阻挡的候选会被跳过并继续检查下一名
        /// </summary>
        public bool TrySelectTarget(
            out IMeleeTarget selectedTarget,
            out float selectedDistance,
            out float selectedAngle)
        {
            selectedTarget = null;
            selectedDistance = 0f;
            selectedAngle = 0f;

            if (!isActiveAndEnabled ||
                _character == null ||
                targetSensor == null)
            {
                return false;
            }

            Transform characterRoot =
                _character.transform;

            Vector3 selectionOrigin =
                GetSelectionOrigin();

            int candidateCount =
                _selector.CopyRankedCandidates(
                    targetSensor,
                    characterRoot,
                    selectionOrigin,
                    characterRoot.forward,
                    maxAngleFromForward,
                    _rankedCandidates);

            if (candidateCount <= 0)
            {
                return false;
            }

            PhysicsScene physicsScene = gameObject.scene.GetPhysicsScene();

            for (int i = 0;
                 i < _rankedCandidates.Count;
                 i++)
            {
                MeleeTargetCandidate candidate =
                    _rankedCandidates[i];

                bool hasLineOfSight =
                    _lineOfSightChecker.HasLineOfSight(
                        physicsScene,
                        characterRoot,
                        selectionOrigin,
                        candidate.Target,
                        lineOfSightLayers,
                        lineOfSightRadius,
                        lineOfSightTriggerInteraction,
                        out _);

                if (!hasLineOfSight)
                {
                    continue;
                }

                selectedTarget = candidate.Target;
                selectedDistance = candidate.HorizontalDistance;
                selectedAngle = candidate.Angle;

                return true;
            }

            return false;
        }

        /// <summary>
        /// 选择无遮挡目标并创建一次带目标快照的近战吸附意图
        /// 最大位移由具体攻击配置传入，选敌控制器不决定武器规则
        /// </summary>
        public bool TryCreateAdsorptionIntent(
            uint attackTick,
            float maximumTravelDistance,
            out MeleeIntent intent)
        {
            intent = default;

            bool invalidMaximumDistance =
                maximumTravelDistance <= 0f ||
                float.IsNaN(maximumTravelDistance) ||
                float.IsInfinity(maximumTravelDistance);

            if (invalidMaximumDistance)
            {
                return false;
            }

            if (!TrySelectTarget(
                    out IMeleeTarget target,
                    out _,
                    out _))
            {
                return false;
            }

            intent = new MeleeIntent(
                target,
                attackTick,
                target.AdsorptionPosition,
                target.CollisionRadius,
                maximumTravelDistance);

            return intent.IsValid;
        }

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            CacheReferences();

            if (_character == null)
            {
                Debug.LogError(
                    "[近战选敌] 当前物体没有 NiumaCharacterController",
                    this);

                enabled = false;
                return;
            }

            if (targetSensor == null)
            {
                Debug.LogError(
                    "[近战选敌] 角色子物体中没有找到 MeleeTargetSensor",
                    this);

                enabled = false;
            }
        }

        /// <summary>
        /// 在编辑器中限制选敌角度和视线扫掠半径并自动补全组件引用
        /// </summary>
        private void OnValidate()
        {
            maxAngleFromForward = Mathf.Clamp(
                maxAngleFromForward,
                0f,
                180f);

            lineOfSightRadius = Mathf.Max(
                0f,
                lineOfSightRadius);

            CacheReferences();
        }

        #endregion

        #region Helpers

        private void CacheReferences()
        {
            if (_character == null)
            {
                _character = GetComponent<NiumaCharacterController>();
            }

            if (targetSensor == null)
            {
                targetSensor = GetComponentInChildren<MeleeTargetSensor>(true);
            }
        }

        private Vector3 GetSelectionOrigin()
        {
            CharacterController controller = _character.CharacterController;

            if (controller != null && controller.enabled)
            {
                return controller.bounds.center;
            }

            return _character.transform.position;
        }

        #endregion
    }
}
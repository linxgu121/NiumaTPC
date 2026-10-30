using NiumaTPC.Character.Combat;
using NiumaTPC.Enemy.Brain;
using UnityEngine;

namespace NiumaTPC.Enemy.Combat
{
    /// <summary>
    /// 将普通怪物的EnemyAttackProfileSO适配为公共近战攻击来源
    /// 不负责决策、动画、命中查询或伤害结算
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EnemyBrain))]
    public sealed class EnemyMeleeAttackSource : MonoBehaviour, IMeleeAttackSource
    {
        #region Inspector(面板配置)

        [SerializeField]
        [Tooltip("提供敌人定义与攻击配置的EnemyBrain")]
        private EnemyBrain brain;

        #endregion

        #region Public API(公共接口)

        public EnemyBrain Brain => brain;

        public EnemyAttackProfileSO AttackProfile =>
            brain != null && brain.Definition != null
                ? brain.Definition.AttackProfile
                : null;

        /// <summary>
        /// 检查怪物攻击配置能否为指定通道生成固定Tick攻击定义
        /// 缺少Brain、敌人定义或攻击配置时不能提供攻击
        /// </summary>
        public bool TryGetAttackDefinition(
            MeleeAttackType attackType,
            float tickDeltaTime,
            out MeleeAttackDefinition definition)
        {
            definition = default;

            if (!isActiveAndEnabled || brain == null || brain.Definition == null)
            {
                return false;
            }

            EnemyAttackProfileSO attackProfile = brain.Definition.AttackProfile;

            if (attackProfile == null)
            {
                return false;
            }

            return attackProfile.TryCreateDefinition(attackType, tickDeltaTime, out definition);
        }

        #endregion

        #region Unity Lifecycle(Unity 生命周期)

        private void Reset()
        {
            brain = GetComponent<EnemyBrain>();
        }

        private void Awake()
        {
            if (brain == null)
            {
                brain = GetComponent<EnemyBrain>();
            }

            if (brain == null)
            {
                Debug.LogError("[敌人攻击来源] 当前物体没有EnemyBrain，无法解析怪物攻击配置", this);
                enabled = false;
            }
        }

        #endregion
    }
}
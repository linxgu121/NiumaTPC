using System;
using NiumaTPC.Character.Combat;
using NiumaTPC.Character.ProcessingPipeline;
using NiumaTPC.Core.Combat;
using NiumaTPC.Enemy.Config;
using UnityEngine;

namespace NiumaTPC.Enemy.Core
{
    /// <summary>
    /// 普通怪物的基础生命与受伤核心
    /// 不依赖玩家输入、状态机、装备和IK系统
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BasicEnemyActor : MonoBehaviour, IDamageable
    {
        #region Inspector(面板配置)

        [Header("敌人配置")]

        [SerializeField]
        [Tooltip("当前普通怪物使用的敌人定义")]
        private EnemyDefinitionSO definition;

        #endregion

        #region Runtime State(运行时状态)

        private DamagePipeline _damagePipeline;
        private float _maxHealth;
        private HitSpotDamageModifier _hitSpotDamageModifier;
        private float _currentHealth;
        private bool _initialized;
        private bool _isDead;

        #endregion

        #region Public API(公共接口)

        public EnemyDefinitionSO Definition => definition;
        public DamagePipeline DamagePipeline => _damagePipeline;
        public float MaxHealth => _maxHealth;
        public float CurrentHealth => _currentHealth;
        public bool IsInitialized => _initialized;
        public bool IsDead => _isDead;

        /// <summary>
        /// 普通怪物实际完成一次生命扣除后触发
        /// </summary>
        public event Action<CharacterDamageInfo> OnDamageApplied;

        /// <summary>
        /// 普通怪物生命首次归零时触发
        /// </summary>
        public event Action<CharacterDeathInfo> OnDied;

        /// <summary>
        /// 将普通怪物恢复到初始生命状态
        /// 供首次生成和后续对象池复用调用
        /// </summary>
        public void ResetRuntimeState()
        {
            if (!_initialized) return;

            _currentHealth = _maxHealth;
            _isDead = false;
        }

        #endregion

        #region Unity Lifecycle(Unity 生命周期)

        private void Awake()
        {
            _initialized = TryInitialize();

            if (!_initialized)
            {
                enabled = false;
                return;
            }

            ResetRuntimeState();
        }

        #endregion

        #region Initialization(初始化)

        /// <summary>
        /// 检查敌人定义与基础生命并建立伤害管线
        /// 缺少必要配置时阻止普通怪物进入运行状态
        /// </summary>
        private bool TryInitialize()
        {
            if (definition == null)
            {
                Debug.LogError("[普通怪物] 缺少EnemyDefinitionSO", this);
                return false;
            }

            if (float.IsNaN(definition.BaseMaxHealth) ||
                float.IsInfinity(definition.BaseMaxHealth) ||
                definition.BaseMaxHealth <= 0f)
            {
                Debug.LogError($"[普通怪物] 基础最大生命不合法：Value={definition.BaseMaxHealth}", this);
                return false;
            }

            _maxHealth = definition.BaseMaxHealth;
            _damagePipeline = new DamagePipeline();
            _hitSpotDamageModifier = new HitSpotDamageModifier(definition.HitSpotDamageProfile);
            _damagePipeline.Register(_hitSpotDamageModifier);
            return true;
        }

        #endregion

        #region Damage(IDamageable伤害入口)

        /// <summary>
        /// 校验伤害请求并通过目标侧伤害管线完成生命扣除
        /// 已死亡或未完成初始化的普通怪物拒绝后续伤害
        /// </summary>
        public void RequestDamage(in DamageRequest request)
        {
            if (!_initialized || _isDead || !request.IsValid) return;

            DamageCalculationResult calculation = _damagePipeline.Calculate(in request);
            if (!calculation.IsApplied) return;

            float healthBefore = Mathf.Max(0f, _currentHealth);
            float healthAfter = Mathf.Max(0f, healthBefore - calculation.FinalDamage);
            float appliedDamage = healthBefore - healthAfter;

            if (appliedDamage <= 0f) return;

            _currentHealth = healthAfter;

            var damageInfo = new CharacterDamageInfo(
                in calculation.Request,
                calculation.BaseDamage,
                calculation.FinalDamage,
                appliedDamage,
                healthBefore,
                healthAfter);

            OnDamageApplied?.Invoke(damageInfo);

            if (healthAfter > 0f) return;

            _isDead = true;

            var deathInfo = new CharacterDeathInfo(in calculation.Request, appliedDamage);
            OnDied?.Invoke(deathInfo);
        }

        #endregion
    }
}

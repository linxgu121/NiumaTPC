using System;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using NiumaTPC.Character;
using NiumaTPC.Core.Combat;
using UnityEngine;

namespace NiumaTPC.FishNet.Combat
{
    /// <summary>
    /// 将服务器权威生命值同步给当前网络对象的观察者
    /// 不负责计算伤害、死亡或播放受击表现
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NiumaCharacterController))]
    public sealed class NetworkHealthBridge : NetworkBehaviour
    {
        #region Inspector(面板配置)

        [SerializeField]
        [Tooltip("当前网络角色，未绑定时自动获取")]
        private NiumaCharacterController character;

        #endregion

        #region Network State(网络状态)

        private readonly SyncVar<float> _maxHealth = new();
        private readonly SyncVar<float> _currentHealth = new();

        #endregion

        #region Public API(公共接口)

        public float MaxHealth => _maxHealth.Value;
        public float CurrentHealth => _currentHealth.Value;
        public bool IsDead => _currentHealth.Value <= 0f;

        /// <summary>
        /// 本地收到生命同步结果时触发
        /// 参数依次为当前生命和最大生命
        /// </summary>
        public event Action<float, float> OnHealthChanged;

        #endregion

        #region Unity Lifecycle(Unity生命周期)

        private void Awake()
        {
            if (character == null) character = GetComponent<NiumaCharacterController>();

            // 生命值变化需要尽快同步，不进行固定时间合并
            _maxHealth.UpdateSendRate(0f);
            _currentHealth.UpdateSendRate(0f);

            _maxHealth.OnChange += HandleMaxHealthChanged;
            _currentHealth.OnChange += HandleCurrentHealthChanged;
        }

        private void OnDestroy()
        {
            _maxHealth.OnChange -= HandleMaxHealthChanged;
            _currentHealth.OnChange -= HandleCurrentHealthChanged;

            if (character != null)
            {
                character.OnDamageApplied -= HandleDamageApplied;
            }
        }

        #endregion

        #region FishNet Lifecycle(FishNet生命周期)

        public override void OnStartServer()
        {
            base.OnStartServer();

            if (character == null) character = GetComponent<NiumaCharacterController>();
            if (character == null || character.RuntimeData == null || character.BaseStats == null)
            {
                Debug.LogError("[网络生命] 角色尚未完成初始化，无法同步生命值", this);
                return;
            }

            character.OnDamageApplied -= HandleDamageApplied;
            character.OnDamageApplied += HandleDamageApplied;

            _maxHealth.Value = character.BaseStats.MaxHealth;
            _currentHealth.Value = character.RuntimeData.CurrentHealth;
        }

        public override void OnStopServer()
        {
            if (character != null)
            {
                character.OnDamageApplied -= HandleDamageApplied;
            }

            base.OnStopServer();
        }

        public override void OnStartClient()
        {
            base.OnStartClient();

            ApplyClientHealth();
            PublishHealth();
        }

        #endregion

        #region Server Damage(服务器伤害)

        /// <summary>
        /// 将服务器已经完成扣除的生命值写入网络状态
        /// </summary>
        private void HandleDamageApplied(CharacterDamageInfo damageInfo)
        {
            if (!IsServerInitialized) return;

            _currentHealth.Value = damageInfo.HealthAfter;
        }

        #endregion

        #region Client Presentation(客户端表现)

        private void HandleMaxHealthChanged(float previous, float current, bool asServer)
        {
            if (!asServer) ApplyClientHealth();
            if (!asServer && IsHostStarted) return;

            PublishHealth();
        }

        private void HandleCurrentHealthChanged(float previous, float current, bool asServer)
        {
            if (!asServer) ApplyClientHealth();
            if (!asServer && IsHostStarted) return;

            PublishHealth();
        }

        /// <summary>
        /// 将同步值写入客户端只读运行时副本
        /// 不在客户端执行伤害管线
        /// </summary>
        private void ApplyClientHealth()
        {
            if (character == null || character.RuntimeData == null) return;

            character.RuntimeData.CurrentHealth = _currentHealth.Value;
        }

        private void PublishHealth()
        {
            OnHealthChanged?.Invoke(_currentHealth.Value, _maxHealth.Value);
        }

        #endregion
    }
}

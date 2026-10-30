using NiumaTPC.Character;
using NiumaTPC.Character.Combat;
using NiumaTPC.Core.Object;
using UnityEngine;

namespace NiumaTPC.Item
{
    /// <summary>
    /// 通用近战武器行为
    /// 负责装备、待机和攻击动画表现
    /// 攻击成立、冷却与吸附由固定 Tick 网络模拟负责
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MeleeWeaponBehaviour : MonoBehaviour, IHoldableItem, IPoolable
    {
        #region Inspector

        [SerializeField]
        [Tooltip("双手武器的左手辅助握点，通常放在武器副握柄位置")]
        private Transform _leftHandGoal;

        #endregion

        #region Runtime State

        private NiumaCharacterController _player;

        private ItemInstance _instance;

        private MeleeWeaponSO _config;

        private bool _isEquipping;

        private float _equipEndTime;

        private bool _leftHandIKActive;

        private float _ikEnableTime;

        /// <summary>
        /// 当前攻击动画结束回调所在层
        /// 负数表示没有已注册的攻击回调
        /// </summary>
        private int _activeAttackCallbackLayer = -1;

        /// <summary>
        /// 最近一次已经播放表现的攻击序号
        /// 使用序号避免固定 Tick 与渲染帧顺序不同导致攻击事件丢失
        /// </summary>
        private uint _lastPlayedAttackSequence;

        #endregion

        #region IHoldableItem

        public void Initialize(ItemInstance instanceData)
        {
            _instance = instanceData;

            _config = instanceData?.BaseData as MeleeWeaponSO;
        }

        public void OnEquipEnter(
            NiumaCharacterController player)
        {
            ClearAttackEndCallback();

            DisableLeftHandIK();

            _player = player;

            _lastPlayedAttackSequence = _player != null && _player.RuntimeData != null ? _player.RuntimeData.SimulationMeleeAttackSequence : 0u;

            _leftHandIKActive = false;

            float ikEnableDelay = _config != null ? Mathf.Max(0f, _config.EnableIKTime) : 0f;

            _ikEnableTime = Time.time + ikEnableDelay;

            _isEquipping = true;

            float equipDuration = _config != null ? Mathf.Max(0f, _config.EquipEndTime) : 0f;

            _equipEndTime = Time.time + equipDuration;

            if (_player != null &&
                _config != null &&
                _config.EquipAnim != null)
            {
                _player.AnimationFacade.PlayTransition(
                    _config.EquipAnim,
                    _config.EquipAnimPlayOptions);
            }

            if (equipDuration <= 0f)
            {
                CompleteEquip();
            }
        }

        public void OnUpdateLogic()
        {
            if (!IsCurrentItem())
            {
                return;
            }

            TryEnableLeftHandIK();

            uint attackSequence = _player.RuntimeData.SimulationMeleeAttackSequence;

            bool hasNewAttack = attackSequence != 0u && attackSequence != _lastPlayedAttackSequence;

            if (hasNewAttack)
            {
                /*
                 * 先记录序号再检查表现配置
                 * 即使动画资源配置错误也不能在每个渲染帧重复尝试
                 */
                _lastPlayedAttackSequence = attackSequence;

                bool isRecentAttack = _player.RuntimeData.SimulationMeleeAttackCooldownTicksRemaining > 0u;

                if (isRecentAttack)
                {
                    _isEquipping = false;

                    PlayAttack();

                    return;
                }
            }

            if (!_isEquipping || Time.time < _equipEndTime)
            {
                return;
            }

            CompleteEquip();
        }

        public void OnForceUnequip()
        {
            ClearAttackEndCallback();

            DisableLeftHandIK();

            _isEquipping = false;

            if (_player == null ||
                _player.RuntimeData == null ||
                _config == null ||
                _config.UnEquipAnim == null)
            {
                return;
            }

            /*
             * CurrentItem 已经切到另一件物品时不播放旧武器动画
             * 真正收起至空手时才播放卸下表现
             */
            if (_player.RuntimeData.CurrentItem == null)
            {
                _player.AnimationFacade.PlayTransition(_config.UnEquipAnim, _config.UnEquipAnimPlayOptions);
            }
        }

        #endregion

        #region Animation

        private void CompleteEquip()
        {
            _isEquipping = false;

            PlayIdle();
        }

        private void PlayAttack()
        {
            if (_player == null ||
                _player.RuntimeData == null ||
                _config == null)
            {
                return;
            }

            MeleeAttackType attackType =
                _player.RuntimeData.SimulationMeleeAttackType;

            byte comboStepIndex =
                _player.RuntimeData.SimulationMeleeComboStepIndex;

            MeleeComboSO combo = _config.GetCombo(attackType);

            if (combo == null ||
                !combo.TryGetStep(
                    comboStepIndex,
                    out MeleeComboStep step))
            {
                return;
            }

            /*
             * 连招不同段可以使用不同动画层
             * 播放新段前必须清除上一段所在层的旧回调
             */
            ClearAttackEndCallback();

            _player.AnimationFacade.PlayTransition(
                step.AttackAnim,
                step.AttackAnimPlayOptions);

            _activeAttackCallbackLayer =
                step.AttackAnimPlayOptions.Layer;

            _player.AnimationFacade.SetOnEndCallback(
                HandleAttackAnimationEnded,
                _activeAttackCallbackLayer);
        }

        private void HandleAttackAnimationEnded()
        {
            // AnimancerFacade 调用业务回调前已经移除注册
            _activeAttackCallbackLayer = -1;

            if (!IsCurrentItem())
            {
                return;
            }

            PlayIdle();
        }

        private void PlayIdle()
        {
            if (_player == null ||
                _config == null ||
                _config.EquipIdleAnim == null)
            {
                return;
            }

            _player.AnimationFacade.PlayTransition(
                _config.EquipIdleAnim,
                _config.EquipIdleAnimPlayOptions);
        }

        private void ClearAttackEndCallback()
        {
            if (_player == null ||
                _activeAttackCallbackLayer < 0)
            {
                return;
            }

            _player.AnimationFacade.ClearOnEndCallback(
                _activeAttackCallbackLayer);

            _activeAttackCallbackLayer = -1;
        }

        #endregion

        #region IK

        // 检查武器配置、握点和装备身份后启用左手辅助握持
        private void TryEnableLeftHandIK()
        {
            if (_leftHandIKActive ||
                _config == null ||
                !_config.EnableIK ||
                _leftHandGoal == null ||
                _player == null ||
                _player.RuntimeData == null ||
                Time.time < _ikEnableTime ||
                !IsCurrentItem())
            {
                return;
            }

            _player.RuntimeData.LeftHandGoal = _leftHandGoal;
            _player.RuntimeData.WantsLeftHandIK = true;

            _leftHandIKActive = true;
        }

        private void DisableLeftHandIK()
        {
            if (_player != null &&
                _player.RuntimeData != null &&
                _leftHandGoal != null &&
                _player.RuntimeData.LeftHandGoal == _leftHandGoal)
            {
                _player.RuntimeData.WantsLeftHandIK = false;
                _player.RuntimeData.LeftHandGoal = null;
            }

            _leftHandIKActive = false;
            _ikEnableTime = 0f;
        }

        #endregion

        #region Validation

        // 检查该行为仍对应角色当前装备实例
        private bool IsCurrentItem()
        {
            return isActiveAndEnabled &&
                   _player != null &&
                   _player.RuntimeData != null &&
                   _instance != null &&
                   _config != null &&
                   ReferenceEquals(_player.RuntimeData.CurrentItem, _instance);
        }

        #endregion

        #region Pooling

        public void OnSpawned()
        {
            ResetRuntimeState();
        }

        public void OnDespawned()
        {
            ResetRuntimeState();
        }

        private void OnDisable()
        {
            ResetRuntimeState();
        }

        private void ResetRuntimeState()
        {
            ClearAttackEndCallback();

            _activeAttackCallbackLayer = -1;

            _lastPlayedAttackSequence = 0u;

            DisableLeftHandIK();

            _isEquipping = false;
            _equipEndTime = 0f;

            _player = null;
            _instance = null;
            _config = null;
        }

        #endregion
    }
}

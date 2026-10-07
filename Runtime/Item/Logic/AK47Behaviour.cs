using NiumaTPC.Character;
using NiumaTPC.Core.Object;
using UnityEngine;

namespace NiumaTPC.Item
{
    // 步枪AK47行为 负责装备瞄准开火IK后坐力等
    public class AK47Behaviour : MonoBehaviour, IHoldableItem, IPoolable
    {
        [Header("--- 表现与挂点 ---")]
        // 左手握点
        [Tooltip("左手应该握在哪里")]
        [SerializeField] private Transform _leftHandGoal;
        // 枪口火焰
        [Tooltip("枪口火焰特效")]
        [SerializeField] private ParticleSystem _muzzleFlash;
        // 枪口投射点
        [Tooltip("枪口瞄准参考点")]
        [SerializeField] private Transform _muzzle;
        // 玩家引用
        private NiumaCharacterController _player;
        // 实例数据
        private ItemInstance _instance;
        // 配置
        private GunWeaponSO _akconfig;
        // 装备时从当前持有者取得，不能跨对象池复用保留旧角色引用
        private OfflineWeaponFireSource _fireSource;

        // 网络角色提供此接口，枪械本身不引用 FishNet
        private INetworkWeaponFireRequestSender _networkFireSender;

        // 当前模型这次被装备时取得的代次
        private uint _equipmentRevision;

        // 装备状态
        private bool _isEquipping;
        private float _equipEndTime;
        // 上一帧瞄准状态
        private bool _wasAiming;
        // IK调度
        private bool _ikEnableScheduled;
        private float _ikEnableTimePoint;
        private bool _ikDisableScheduled;
        private float _ikDisableTimePoint;

        // 初始化实例和配置
        public void Initialize(ItemInstance instanceData)
        {
            _instance = instanceData;
            _akconfig = instanceData?.BaseData as GunWeaponSO;
        }

        // 装备并设置IK
        public void OnEquipEnter(NiumaCharacterController player)
        {
            _player = player;
            _equipmentRevision = _player != null && _player.RuntimeData != null ? _player.RuntimeData.EquipmentRevision : 0u;
            _fireSource = _player != null ? _player.GetComponent<OfflineWeaponFireSource>() : null;
            _networkFireSender = _player != null ? player.GetComponent<INetworkWeaponFireRequestSender>() : null;
            _isEquipping = true;
            if (_leftHandGoal != null && _player != null && _player.RuntimeData != null)
            {
                _player.RuntimeData.LeftHandGoal = _leftHandGoal;
                _player.RuntimeData.WantsLeftHandIK = false;
                if (_akconfig != null)
                {
                    _ikEnableScheduled = true;
                    _ikEnableTimePoint = Time.time + _akconfig.EnableIKTime;
                }
                else
                {
                    _player.RuntimeData.WantsLeftHandIK = true;
                }
            }

            // 立刻设置 muzzle，不等瞄准时再设置
            // 这样 FinalIK 始终有有效的引用，避免切装备时 NullRef
            if (_muzzle != null && _player != null && _player.RuntimeData != null)
            {
                _player.RuntimeData.CurrentAimReference = _muzzle;
            }

            float equipAnimDuration = _akconfig.EquipEndTime;
            _equipEndTime = Time.time + equipAnimDuration;
            if (_akconfig != null && _akconfig.EquipAnim != null && _player != null)
            {
                _player.AnimationFacade.PlayTransition(_akconfig.EquipAnim, _akconfig.EquipAnimPlayOptions);
            }
        }

        // 每帧更新逻辑
        public void OnUpdateLogic()
        {
            if (_ikEnableScheduled && Time.time >= _ikEnableTimePoint)
            {
                if (_isEquipping)
                {
                    _ikEnableTimePoint = _equipEndTime + 0.001f;
                }
                else
                {
                    _ikEnableScheduled = false;
                    if (_player != null && _player.RuntimeData != null && _player.RuntimeData.CurrentItem == _instance)
                    {
                        _player.RuntimeData.WantsLeftHandIK = true;
                    }
                }
            }
            if (_ikDisableScheduled && Time.time >= _ikDisableTimePoint)
            {
                _ikDisableScheduled = false;
                if (_player != null && _player.RuntimeData != null)
                {
                    var current = _player.RuntimeData.CurrentItem;
                    if (current == null || current.InstanceID == _instance.InstanceID)
                    {
                        _player.RuntimeData.WantsLeftHandIK = false;
                        _player.RuntimeData.LeftHandGoal = null;
                    }
                }
            }
            if (_isEquipping)
            {
                if (Time.time >= _equipEndTime)
                {
                    _isEquipping = false;
                    if (_akconfig != null && _akconfig.EquipIdleAnim != null && _player != null)
                    {
                        _player.AnimationFacade.PlayTransition(_akconfig.EquipIdleAnim, _akconfig.EquipIdleAnimPlayOptions);
                    }
                }
                else
                {
                    return;
                }
            }
            bool isAiming = _player != null && _player.RuntimeData != null && _player.RuntimeData.IsAiming;
            if (!_isEquipping && _wasAiming != isAiming)
            {
                if (isAiming)
                {
                    if (_akconfig != null && _akconfig.AimAnim != null && _player != null)
                    {
                        _player.AnimationFacade.PlayTransition(_akconfig.AimAnim, _akconfig.AnimPlayOptions);
                    }
                    if (_player != null && _player.RuntimeData != null)
                    {
                        // 瞄准时只改意图，muzzle 已在装备时设置
                        _player.RuntimeData.WantsLookAtIK = true;
                    }
                }
                else
                {
                    if (_akconfig != null && _akconfig.EquipIdleAnim != null && _player != null)
                    {
                        _player.AnimationFacade.PlayTransition(_akconfig.EquipIdleAnim, _akconfig.EquipIdleAnimPlayOptions);
                    }
                    if (_player != null && _player.RuntimeData != null)
                    {
                        //仅改意图，不清理 CurrentAimReference
                        _player.RuntimeData.WantsLookAtIK = false;
                    }
                }
                _wasAiming = isAiming;
            }
            bool isFiring = _player != null && _player.RuntimeData != null &&
                           _player.RuntimeData.CurrentItem == _instance &&
                           _player.RuntimeData.WantsToFire;
            if (isAiming && isFiring)
            {
                TryFire();
            }
        }

        // 强制卸载
        public void OnForceUnequip()
        {
            _isEquipping = false;
            _equipmentRevision = 0u;
            if (_muzzleFlash != null) _muzzleFlash.Stop();

            if (_akconfig != null)
            {
                _ikDisableScheduled = true;
                _ikDisableTimePoint = Time.time + _akconfig.DisableIKTime;
            }

            if (_player != null && _player.RuntimeData != null && _player.RuntimeData.CurrentItem == null)
            {
                if (_akconfig != null && _akconfig.UnEquipAnim != null)
                {
                    _player.AnimationFacade.PlayTransition(_akconfig.UnEquipAnim, _akconfig.UnEquipAnimPlayOptions);
                }
            }
        }

        // 检查冷却并开火
        private void TryFire()
        {
            if (_isEquipping || _player == null || _akconfig == null)
            {
                return;
            }

            // 有网络发送组件的角色始终走网络
            // 发送失败、断线或组件禁用，都不能回退到离线射击
            if (_networkFireSender != null)
            {
                // 缓存组件被销毁时也只停止，不转入离线分支
                if (_networkFireSender is MonoBehaviour senderComponent &&
                    senderComponent != null)
                {
                    _networkFireSender.TrySubmitFire(
                        _instance,
                        _equipmentRevision);
                }

                // 提交成功不等于开火成功
                // 本步不执行下方离线登记、枪焰、音效或后坐力
                return;
            }

            // 没有网络发送组件的离线角色，保持原有发射流程
            if (_fireSource == null)
            {
                return;
            }

            if (!_fireSource.TryCreateRequest(
                    _equipmentRevision,
                    out WeaponFireRequest request))
            {
                return;
            }

            if (!_fireSource.TryFire(
                    _instance,
                    request,
                    out _,
                    out _))
            {
                return;
            }

            if (_muzzleFlash != null)
            {
                _muzzleFlash.Play();
            }

            if (_akconfig != null && _akconfig.ShootSound != null && _muzzle != null)
            {
                AudioSource.PlayClipAtPoint(_akconfig.ShootSound, _muzzle.position);
            }

            if (_akconfig != null && _akconfig.MuzzleVFXPrefab != null && _muzzle != null)
            {
                GameObject muzzleVFX;
                if (SimpleObjectPoolSystem.Shared != null)
                {
                    muzzleVFX = SimpleObjectPoolSystem.Shared.Spawn(_akconfig.MuzzleVFXPrefab);
                    muzzleVFX.transform.SetPositionAndRotation(_muzzle.position, _muzzle.rotation);
                    muzzleVFX.transform.SetParent(_muzzle, true);
                }
                else
                {
                    muzzleVFX = Object.Instantiate(_akconfig.MuzzleVFXPrefab, _muzzle.position, _muzzle.rotation);
                    muzzleVFX.transform.parent = _muzzle;
                }
            }

            ApplyRecoil();

        }

        // 应用后坐力
        private void ApplyRecoil()
        {
            if (_player == null || _player.RuntimeData == null || _akconfig == null) return;
            float pitchNoise = Random.Range(-_akconfig.RecoilPitchRandomRange, _akconfig.RecoilPitchRandomRange);
            float yawNoise = Random.Range(-_akconfig.RecoilYawRandomRange, _akconfig.RecoilYawRandomRange);
            float finalPitch = _akconfig.RecoilPitchAngle + pitchNoise;
            float finalYaw = _akconfig.RecoilYawAngle + yawNoise;
            float yawSign = Random.value > 0.5f ? 1f : -1f;
            _player.RuntimeData.ViewPitch -= finalPitch;
            _player.RuntimeData.ViewYaw += yawSign * finalYaw;
            _player.RuntimeData.ViewPitch = Mathf.Clamp(
                _player.RuntimeData.ViewPitch,
                _player.Config.Core.PitchLimits.x,
                _player.Config.Core.PitchLimits.y
            );
        }

        public void OnSpawned()
        {
            // 保守复位（避免复用武器时残留）
            _isEquipping = false;
            _wasAiming = false;
            _ikEnableScheduled = false;
            _ikDisableScheduled = false;

            _fireSource = null;
            _networkFireSender = null;
            _equipmentRevision = 0u;
            _player = null;

            if (_muzzleFlash != null) _muzzleFlash.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        }

        public void OnDespawned()
        {
            if (_muzzleFlash != null)
            {
                _muzzleFlash.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }

            _fireSource = null;
            _networkFireSender = null;
            _equipmentRevision = 0u;
            _player = null;

        }
    }
}
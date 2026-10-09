using NiumaTPC.Character;
using NiumaTPC.Core.Object;
using UnityEngine;

namespace NiumaTPC.Item
{
    // 步枪AK47行为 负责装备瞄准开火IK后坐力等
    public class AK47Behaviour : MonoBehaviour, IHoldableItem, IPoolable, IWeaponFirePresentation
    {
        [Header("--- 表现与挂点 ---")]
        // 左手握点
        [Tooltip("左手应该握在哪里")]
        [SerializeField] private Transform _leftHandGoal;
        // 枪口投射点
        [Tooltip("枪口瞄准参考点")]
        [SerializeField] private Transform _muzzle;
        // 玩家引用
        private NiumaCharacterController _player;
        // 实例数据
        private ItemInstance _instance;
        // 配置
        private GunWeaponSO _akconfig;
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

        #region 枪口表现运行状态

        // 当前这次装备持有一份枪焰对象，连续开火时复用
        private GameObject _muzzleVfxObject;
        private SimpleObjectPoolSystem _muzzleVfxPool;

        private ParticleSystem[] _muzzleParticles = System.Array.Empty<ParticleSystem>();

        // 配置或对象池缺失时，避免每发重复尝试并刷日志
        private bool _muzzleSetupAttempted;

        #endregion

        // 初始化实例和配置
        public void Initialize(ItemInstance instanceData)
        {
            _instance = instanceData;
            _akconfig = instanceData?.BaseData as GunWeaponSO;
        }

        // 装备并设置IK
        public void OnEquipEnter(NiumaCharacterController player)
        {
            ReleaseMuzzle();

            _player = player;
            _equipmentRevision = _player != null && _player.RuntimeData != null ? _player.RuntimeData.EquipmentRevision : 0u;
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

            ReleaseMuzzle();

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

        // 单机与多人均提交网络意图，冷却和实际发射由服务器处理
        private void TryFire()
        {
            if (_isEquipping || _player == null || _akconfig == null)
            {
                return;
            }

            // 单机也运行本地 Host，不保留没有网络组件时的离线兜底
            // 接口引用不会使用 Unity 的销毁判空，需要检查实际组件
            if (!(_networkFireSender is MonoBehaviour senderComponent) ||
                senderComponent == null ||
                !senderComponent.isActiveAndEnabled)
            {
                return;
            }

            // 网络驱动负责本地预表现，服务器仍决定是否真正发射
            _networkFireSender.TrySubmitFire(_instance, _equipmentRevision);
        }

        #region 开火表现

        public bool TryPlayFire(
            ItemInstance expectedItem,
            bool applyRecoil)
        {
            var data = _player != null ? _player.RuntimeData : null;

            if (!isActiveAndEnabled ||
                expectedItem == null ||
                data == null ||
                _akconfig == null ||
                _equipmentRevision == 0u ||
                _equipmentRevision != data.EquipmentRevision ||
                !ReferenceEquals(_instance, expectedItem) ||
                !ReferenceEquals(data.CurrentItem, _instance))
            {
                return false;
            }

            // 表现失败不能撤销服务器已经提交的射击
            PlayMuzzle();

            if (_akconfig.ShootSound != null && _muzzle != null)
            {
                AudioSource.PlayClipAtPoint(
                    _akconfig.ShootSound,
                    _muzzle.position);
            }

            // 观察别人的射击时，不改变自己的摄像机
            if (applyRecoil)
            {
                ApplyRecoil();
            }

            return true;
        }

        private void PlayMuzzle()
        {
            if (_muzzleVfxObject == null)
            {
                if (_muzzleSetupAttempted)
                {
                    return;
                }

                _muzzleSetupAttempted = true;

                // 留空表示这把枪不需要枪焰
                if (_akconfig.MuzzleVFXPrefab == null || _muzzle == null)
                {
                    return;
                }

                _muzzleVfxPool = SimpleObjectPoolSystem.Shared;

                if (_muzzleVfxPool == null)
                {
                    Debug.LogWarning(
                        "[枪械表现] 没有可用对象池，跳过枪口特效",
                        this);
                    return;
                }

                _muzzleVfxObject =
                    _muzzleVfxPool.Spawn(_akconfig.MuzzleVFXPrefab);

                if (_muzzleVfxObject == null)
                {
                    return;
                }

                _muzzleParticles =
                    _muzzleVfxObject.GetComponentsInChildren<ParticleSystem>(true);

                // 清除复用对象的旧粒子，再调整挂点
                StopMuzzleParticles();

                Transform effectTransform = _muzzleVfxObject.transform;
                effectTransform.SetParent(_muzzle, false);
                effectTransform.localPosition = Vector3.zero;
                effectTransform.localRotation = Quaternion.identity;
                effectTransform.localScale =
                    _akconfig.MuzzleVFXPrefab.transform.localScale;
            }

            // 重新开始本发的短促枪焰，不让旧播放状态影响下一发
            StopMuzzleParticles();

            foreach (ParticleSystem particle in _muzzleParticles)
            {
                if (particle != null && particle.gameObject.activeInHierarchy)
                {
                    // 数组已经包含子粒子，不再递归播放
                    particle.Play(false);
                }
            }
        }

        private void StopMuzzleParticles()
        {
            foreach (ParticleSystem particle in _muzzleParticles)
            {
                if (particle != null)
                {
                    particle.Stop(
                        false,
                        ParticleSystemStopBehavior.StopEmittingAndClear);
                }
            }
        }

        private void ReleaseMuzzle()
        {
            StopMuzzleParticles();

            GameObject instance = _muzzleVfxObject;
            SimpleObjectPoolSystem pool = _muzzleVfxPool;

            // 先清空持有记录，避免停用和回收回调重复处理
            _muzzleVfxObject = null;
            _muzzleVfxPool = null;
            _muzzleParticles = System.Array.Empty<ParticleSystem>();
            _muzzleSetupAttempted = false;

            if (instance == null)
            {
                return;
            }

            // 回收的特效不能继续挂在武器下面
            instance.transform.SetParent(null, true);

            if (pool == null || !pool.TryDespawn(instance))
            {
                Destroy(instance);
            }
        }

        #endregion

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

            // 同时刷新下一次输入采样和相机使用的方向
            var data = _player.RuntimeData;
            data.ViewYaw = Mathf.Repeat(data.ViewYaw, 360f);
            data.AuthorityYaw = data.ViewYaw;
            data.AuthorityPitch = data.ViewPitch;
            data.AuthorityRotation = Quaternion.Euler(
                data.AuthorityPitch,
                data.AuthorityYaw,
                0f);
        }

        #region 池化与清理

        public void OnSpawned()
        {
            ReleaseMuzzle();

            _isEquipping = false;
            _wasAiming = false;
            _ikEnableScheduled = false;
            _ikDisableScheduled = false;

            _networkFireSender = null;
            _equipmentRevision = 0u;

            _player = null;
            _instance = null;
            _akconfig = null;
        }

        public void OnDespawned()
        {
            ReleaseMuzzle();

            _isEquipping = false;
            _wasAiming = false;
            _ikEnableScheduled = false;
            _ikDisableScheduled = false;

            _networkFireSender = null;
            _equipmentRevision = 0u;

            _player = null;
            _instance = null;
            _akconfig = null;
        }

        private void OnDisable()
        {
            ReleaseMuzzle();
        }

        private void OnDestroy()
        {
            ReleaseMuzzle();
        }

        #endregion
    }
}

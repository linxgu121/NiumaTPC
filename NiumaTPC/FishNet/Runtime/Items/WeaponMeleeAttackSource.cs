using NiumaTPC.Character;
using NiumaTPC.Character.Combat;
using NiumaTPC.Character.Simulation;
using NiumaTPC.Item;
using UnityEngine;

namespace NiumaTPC.FishNet.Items
{
    /// <summary>
    /// 将当前确认的MeleeWeaponSO适配为公共近战攻击来源
    /// 不负责装备切换、输入、攻击模拟或命中判定
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NiumaCharacterController))]
    [RequireComponent(typeof(WeaponDriver))]
    public sealed class WeaponMeleeAttackSource : MonoBehaviour, IMeleeAttackSource
    {
        #region Inspector(面板配置)

        [SerializeField]
        [Tooltip("当前网络角色控制器，未绑定时自动获取同物体组件")]
        private NiumaCharacterController character;

        [SerializeField]
        [Tooltip("提供本地确认装备与服务器权威装备的WeaponDriver，未绑定时自动获取同物体组件")]
        private WeaponDriver weaponDriver;

        #endregion

        #region Public API(公共接口)

        public NiumaCharacterController Character => character;

        public WeaponDriver WeaponDriver => weaponDriver;

        /// <summary>
        /// 检查当前确认装备能否为指定攻击通道生成固定Tick攻击定义
        /// 服务器读取权威装备，拥有者客户端读取已经确认的本地装备
        /// </summary>
        public bool TryGetAttackDefinition(
            MeleeAttackType attackType,
            float tickDeltaTime,
            out MeleeAttackDefinition definition)
        {
            definition = default;

            if (!isActiveAndEnabled ||
                !IsPositiveFinite(tickDeltaTime) ||
                !TryResolveCurrentMeleeWeapon(out MeleeWeaponSO meleeWeapon, out uint sourceRevision))
            {
                return false;
            }

            MeleeComboSO combo = meleeWeapon.GetCombo(attackType);

            if (combo == null || combo.StepCount <= 0)
            {
                return false;
            }

            MeleeAdsorptionMotionProfile motionProfile = default;

            if (meleeWeapon.EnableAdsorption &&
                !meleeWeapon.TryCreateAdsorptionMotionProfile(tickDeltaTime, out motionProfile))
            {
                return false;
            }

            definition = new MeleeAttackDefinition(
                attackType,
                combo,
                sourceRevision,
                meleeWeapon.EnableAdsorption,
                meleeWeapon.AdsorptionMaximumTravelDistance,
                in motionProfile);

            return definition.IsValid;
        }

        #endregion

        #region Unity Lifecycle(Unity 生命周期)

        private void Reset()
        {
            character = GetComponent<NiumaCharacterController>();
            weaponDriver = GetComponent<WeaponDriver>();
        }

        private void Awake()
        {
            if (character == null)
            {
                character = GetComponent<NiumaCharacterController>();
            }

            if (weaponDriver == null)
            {
                weaponDriver = GetComponent<WeaponDriver>();
            }

            if (character == null || weaponDriver == null)
            {
                Debug.LogError("[武器攻击来源] 缺少NiumaCharacterController或WeaponDriver", this);
                enabled = false;
            }
        }

        #endregion

        #region Resolution(来源解析)

        /// <summary>
        /// 检查当前网络身份并解析对应的已确认近战武器与装备修订号
        /// Host和服务器始终优先读取服务器权威装备
        /// </summary>
        private bool TryResolveCurrentMeleeWeapon(
            out MeleeWeaponSO meleeWeapon,
            out uint sourceRevision)
        {
            meleeWeapon = null;
            sourceRevision = 0u;

            if (character == null ||
                character.RuntimeData == null ||
                weaponDriver == null)
            {
                return false;
            }

            ItemInstance item;

            if (weaponDriver.IsServerInitialized)
            {
                if (!weaponDriver.TryGetServerEquippedItem(out item, out sourceRevision))
                {
                    return false;
                }
            }
            else
            {
                item = character.RuntimeData.CurrentItem;

                if (!weaponDriver.TryGetConfirmedEquipmentRevision(item, out sourceRevision))
                {
                    return false;
                }
            }

            meleeWeapon = item?.BaseData as MeleeWeaponSO;
            return meleeWeapon != null && sourceRevision != 0u;
        }

        #endregion

        #region Validation(数据校验)

        /// <summary>
        /// 检查固定Tick间隔是否为大于零的有限值
        /// </summary>
        private static bool IsPositiveFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;
        }

        #endregion
    }
}
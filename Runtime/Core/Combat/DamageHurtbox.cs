using UnityEngine;

namespace NiumaTPC.Core.Combat
{
    /// <summary>
    /// 角色身体部位的通用受击区域
    /// 近战、实体子弹和射线武器均从这里读取部位与伤害倍率
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class DamageHurtbox : MonoBehaviour
    {
        #region Inspector

        [Header("身体部位")]

        [SerializeField]
        [Tooltip("当前碰撞体代表的身体区域")]
        private HitSpot hitSpot = HitSpot.Torso;

    
        #endregion

        #region Runtime State

        private Collider _hurtboxCollider;
        private IDamageable _damageable;
        private Component _damageableComponent;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            _hurtboxCollider = GetComponent<Collider>();
            _damageable = GetComponentInParent<IDamageable>();
            _damageableComponent = _damageable as Component;

            if (_damageableComponent == null)
            {
                Debug.LogError(
                    "[受击区域] 父级中没有找到 IDamageable 实现",
                    this);
            }
        }

        #endregion

        #region Public API

        public HitSpot HitSpot => hitSpot;

        public Collider HurtboxCollider => _hurtboxCollider;

        /// <summary>
        /// 同一角色的所有 Hurtbox 都返回相同的受伤组件
        /// 可作为一次攻击内防止重复伤害的目标身份
        /// </summary>
        public Component DamageableComponent => _damageableComponent;

        /// <summary>
        /// 检查受击区域是否仍绑定到有效且启用的受伤组件
        /// 具体死亡免疫仍由角色生命与仲裁系统负责
        /// </summary>
        public bool TryGetDamageable(out IDamageable damageable)
        {
            damageable = null;

            if (_damageableComponent == null ||
                !_damageableComponent.gameObject.activeInHierarchy)
            {
                return false;
            }

            if (_damageableComponent is Behaviour behaviour &&
                !behaviour.isActiveAndEnabled)
            {
                return false;
            }

            damageable = _damageable;
            return damageable != null;
        }

        #endregion
    }
}
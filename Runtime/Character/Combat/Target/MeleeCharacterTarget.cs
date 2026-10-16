
using NiumaTPC.Core.Combat;
using UnityEngine;

namespace NiumaTPC.Character.Combat
{
    /// <summary>
    /// 将角色运行时状态暴露给近战目标选择系统
    /// 我的吸附设计：以玩家为基准，向怪物方向位移吸附
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NiumaCharacterController))]
    public sealed class MeleeCharacterTarget : MonoBehaviour, IMeleeTarget
    {
        private NiumaCharacterController _character;

        #region Public API

        public NiumaCharacterController Character => _character;

        public Transform RootTransform => _character != null ? _character.transform : transform;

        public Vector3 MagnetismPosition
        {
            get
            {
                CharacterController controller =  _character != null ? _character.CharacterController : null;

                if (controller != null && controller.enabled)
                {
                    return controller.bounds.center;
                }

                return transform.position;
            }
        }

        public float CollisionRadius
        {
            get
            {
                CharacterController controller = _character != null ? _character.CharacterController : null;

                if (controller == null || !controller.enabled)
                {
                    return 0f;
                }

                Bounds bounds = controller.bounds;

                return Mathf.Max(bounds.extents.x,bounds.extents.z);
            }
        }

        public bool IsTargetable
        {
            get
            {
                if (!isActiveAndEnabled || _character == null || !_character.isActiveAndEnabled)
                {
                    return false;
                }

                var data = _character.RuntimeData;

                if (data == null)
                {
                    return false;
                }

                return !data.IsDead && !data.Arbitration.IsDead;
            }
        }

        #endregion

        private void Awake()
        {
            _character = GetComponent<NiumaCharacterController>();
        }

        
    }
}
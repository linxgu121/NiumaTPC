using System.Collections.Generic;
using NiumaCore.Module;
using NiumaCore.Player;
using UnityEngine;

namespace NiumaTPC.Module
{
    public sealed class PlayerModuleController : MonoBehaviour, IGameModule, IPlayerModule
    {
        /// <summary>
        /// 角色控制器组件，管理玩家移动与交互逻辑，需在Inspector面板赋值
        /// </summary>
        [SerializeField] private NiumaTPC.Character.NiumaCharacterController character;

        /// <summary>
        /// 模块名称，需唯一且符合命名规范
        /// </summary>
        public string ModuleName => "NiumaTPC.Player";

        /// <summary>
        /// 当前控制状态，true表示玩家可以控制角色，false表示控制被禁用（如在菜单界面或角色死亡时）
        /// </summary>
        public bool IsControlEnabled { get; private set; } = true;

        private readonly HashSet<string> _disableReasons = new HashSet<string>();

        /// <summary>
        /// 玩家角色的Transform组件，供其他模块或系统访问角色位置和旋转信息
        /// </summary>
        public Transform PlayerTransform => character != null ? character.transform : transform;

        public void Initialize(GameContext context)
        {
            
        }

        public void StartModule()
        {
            EnableControl();
        }

        public void StopModule()
        {
            DisableControl("ModuleStopped");
        }

        public void Tick(float deltaTime)
        {
        }

        public void EnableControl()
        {
            EnableControl(null);
        }

        public void EnableControl(string reason)
        {
            if (string.IsNullOrWhiteSpace(reason))
            {
                _disableReasons.Clear();
            }
            else
            {
                _disableReasons.Remove(reason);
            }

            RefreshControlState();
        }

        public void DisableControl(string reason)
        {
            _disableReasons.Add(string.IsNullOrWhiteSpace(reason) ? "Unknown" : reason);
            RefreshControlState();
        }

        public void Teleport(Vector3 position, Quaternion rotation)
        {
            var targetTransform = PlayerTransform;
            if (targetTransform == null) return;

            var cc = targetTransform.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;

            var rb = targetTransform.GetComponent<Rigidbody>();
            if (rb != null)
            {
                // 传送时清理刚体残留速度，避免读档或复活后被上一帧物理速度带走。
                if (!rb.isKinematic)
                {
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                    rb.Sleep();
                }

                rb.position = position;
                rb.rotation = rotation;
            }
            else
            {
                targetTransform.SetPositionAndRotation(position, rotation);
            }

            if (cc != null) cc.enabled = true;
        }

        public void Kill(string reason)
        {
            // 后续接入死亡状态或派发 PlayerDiedEvent
        }

        private void RefreshControlState()
        {
            var shouldEnable = _disableReasons.Count == 0;
            IsControlEnabled = shouldEnable;
            character?.SetInputBlocked(!shouldEnable, true);
        }

    }
}

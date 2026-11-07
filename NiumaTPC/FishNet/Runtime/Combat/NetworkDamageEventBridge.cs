using System;
using FishNet.Object;
using FishNet.Transporting;
using NiumaTPC.Character;
using NiumaTPC.Core.Combat;
using UnityEngine;

namespace NiumaTPC.FishNet.Combat
{
    /// <summary>
    /// 将服务器确认的伤害结算转换成客户端瞬时受击事件
    /// 不同步生命值，不直接播放动画或特效
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NiumaCharacterController))]
    public sealed class NetworkDamageEventBridge : NetworkBehaviour
    {
        #region Inspector(面板配置)

        [SerializeField]
        [Tooltip("当前网络角色，未绑定时自动获取")]
        private NiumaCharacterController character;

        #endregion

        #region Events(事件)

        /// <summary>
        /// 当前客户端收到服务器确认的受击事件时触发
        /// </summary>
        public event Action<NetworkDamageEvent> OnDamageReceived;

        #endregion

        #region Unity Lifecycle(Unity生命周期)

        private void Awake()
        {
            if (character == null) character = GetComponent<NiumaCharacterController>();
        }

        private void OnDestroy()
        {
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
            if (character == null)
            {
                Debug.LogError("[网络受击] 缺少角色控制器，无法广播受击事件", this);
                return;
            }

            character.OnDamageApplied -= HandleDamageApplied;
            character.OnDamageApplied += HandleDamageApplied;
        }

        public override void OnStopServer()
        {
            if (character != null)
            {
                character.OnDamageApplied -= HandleDamageApplied;
            }

            base.OnStopServer();
        }

        #endregion

        #region Server Broadcast(服务器广播)

        /// <summary>
        /// 将服务器已经实际扣除的伤害广播给当前对象的观察者
        /// </summary>
        private void HandleDamageApplied(CharacterDamageInfo damageInfo)
        {
            if (!IsServerInitialized) return;

            DamageRequest request = damageInfo.Request;

            ObserversReceiveDamage(
                request.SourceObjectId,
                request.SourceSequence,
                request.SourceRevision,
                request.HitSpot,
                damageInfo.AppliedDamage,
                damageInfo.IsLethal,
                TimeManager.Tick,
                Channel.Unreliable);
        }

        #endregion

        #region Client Receive(客户端接收)

        [ObserversRpc(
            RunLocally = false,
            BufferLast = false,
            ExcludeOwner = false,
            ExcludeServer = false)]
        private void ObserversReceiveDamage(
            int sourceObjectId,
            ulong sourceSequence,
            uint sourceRevision,
            HitSpot hitSpot,
            float appliedDamage,
            bool isLethal,
            uint serverTick,
            Channel channel = Channel.Unreliable)
        {
            if (!IsClientInitialized) return;

            var damageEvent = new NetworkDamageEvent(
                sourceObjectId,
                sourceSequence,
                sourceRevision,
                hitSpot,
                appliedDamage,
                isLethal,
                serverTick);

            OnDamageReceived?.Invoke(damageEvent);
        }

        #endregion
    }
}

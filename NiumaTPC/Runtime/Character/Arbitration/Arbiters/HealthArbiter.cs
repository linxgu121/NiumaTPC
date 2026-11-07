using NiumaTPC.Character.RuntimeData;
using NiumaTPC.Character.State.Override;
using NiumaTPC.Core.Combat;
using UnityEngine;

namespace NiumaTPC.Character.Arbitration.Arbiters
{
    /// <summary>
    /// 生命值仲裁器
    /// </summary>
    public class HealthArbiter
    {
        private readonly NiumaCharacterController _player;
        private readonly PlayerRuntimeData _data;

        // 环形缓冲区 (最多同时受击 16 次)
        private DamageRequest[] _damageQueue = new DamageRequest[16];
        private int _head;
        private int _tail;
        private int _count;

        public HealthArbiter(NiumaCharacterController player)
        {
            _player = player;
            _data = player.RuntimeData;
        }

        /// <summary>
        /// 内部调用的入队接口
        /// </summary>
        internal void Enqueue(in DamageRequest request)
        {
            if (_data.IsDead) return;

            if (_count == _damageQueue.Length)
            {
                ExpandDamageQueue();
            }

            _damageQueue[_tail] = request;
            _tail = (_tail + 1) % _damageQueue.Length;
            _count++;
        }

        /// <summary>
        /// 清除角色重新生成前遗留的待处理伤害
        /// </summary>
        public void Reset()
        {
            _head = 0;
            _tail = 0;
            _count = 0;
        }

        /// <summary>
        /// 每帧统一裁决
        /// </summary>
        public void Arbitrate()
        {
            if (_data.IsDead || _count == 0) return;

            DamageRequest lethalRequest = default;
            float lethalAppliedDamage = 0f;
            bool hasLethalDamage = false;

            while (_count > 0)
            {
                DamageRequest request = _damageQueue[_head];
                _head = (_head + 1) % _damageQueue.Length;
                _count--;

                DamageCalculationResult calculationResult = _player.DamagePipeline.Calculate(in request);
                if (!calculationResult.IsApplied) continue;

                float damageAmount = calculationResult.FinalDamage;

                float healthBeforeDamage = Mathf.Max(0f, _data.CurrentHealth);
                float healthAfterDamage = Mathf.Max(0f, healthBeforeDamage - damageAmount);
                float appliedDamage = healthBeforeDamage - healthAfterDamage;

                _data.CurrentHealth = healthAfterDamage;

                var damageInfo = new CharacterDamageInfo(
                    in calculationResult.Request,
                    calculationResult.BaseDamage,
                    calculationResult.FinalDamage,
                    appliedDamage,
                    healthBeforeDamage,
                    healthAfterDamage);

                _player.NotifyDamageApplied(in damageInfo);

                if (healthAfterDamage > 0f) continue;

                lethalRequest = calculationResult.Request;
                lethalAppliedDamage = appliedDamage;
                hasLethalDamage = true;

                // 丢弃同一批次中排在致命伤害之后的请求
                _head = _tail;
                _count = 0;
                break;
            }

            if (!hasLethalDamage) return;

            _data.IsDead = true;
            _data.Arbitration.IsDead = true;
            _data.Arbitration.BlockInput = true;
            _data.Arbitration.BlockUpperBody = true;
            _data.Arbitration.BlockFacial = true;
            _data.Arbitration.BlockIK = true;
            _data.Arbitration.BlockInventory = true;

            var death = _player.StateRegistry.GetState<PlayerDeathState>();
            _player.StateMachine.ChangeState(death);

            var deathInfo = new CharacterDeathInfo(in lethalRequest, lethalAppliedDamage);
            _player.NotifyDeath(in deathInfo);
        }

        #region Queue Capacity(队列容量)

        /// <summary>
        /// 扩容伤害队列并保持现有请求的处理顺序
        /// </summary>
        private void ExpandDamageQueue()
        {
            var expandedQueue = new DamageRequest[_damageQueue.Length * 2];

            for (int i = 0; i < _count; i++)
            {
                expandedQueue[i] = _damageQueue[(_head + i) % _damageQueue.Length];
            }

            _damageQueue = expandedQueue;
            _head = 0;
            _tail = _count;
        }

        #endregion
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace NiumaTPC.Item
{
    /// <summary>
    /// 管理一个物理世界中的活动子弹
    /// 由唯一的权威驱动在主线程推进
    /// </summary>
    public class AmmunitionWorld
    {
        /// <summary>
        /// 将纯数值状态与场景引用分开保存
        /// 使用引用类型，便于直接 ref 修改其 State 字段
        /// </summary>
        private sealed class ActiveAmmunition
        {
            public AmmunitionState State;
            public readonly Transform ShooterRoot;

            public ActiveAmmunition(
                AmmunitionState state,
                Transform shooterRoot)
            {
                State = state;
                ShooterRoot = shooterRoot;
            }
        }

        // 当前版本的世界活动子弹上限
        private const int MaxActiveCount = 1024;

        // 绑定这个子弹世界对应的物理场景
        private readonly PhysicsScene _physicsScene;

        // 子弹碰撞层
        private readonly LayerMask _hitMask;
        private readonly QueryTriggerInteraction _triggerInteraction;

        // 创建单个模拟实例
        private readonly AmmunitionSimulation _simulation = new AmmunitionSimulation();

        // 保存当前存活子弹
        private readonly Dictionary<ulong, ActiveAmmunition> _active = new Dictionary<ulong, ActiveAmmunition>(64);

        // 本帧结束的子弹结果
        private readonly List<AmmunitionEndResult> _endedThisTick = new List<AmmunitionEndResult>(16);

        // 包装一次，后续重复返回同一个只读视图
        private readonly IReadOnlyList<AmmunitionEndResult> _endedView;

        // 0 保留为无效编号
        private ulong _nextShotId = 1;

        // 对外暴露当前存活子弹数量
        public int ActiveCount => _active.Count;

        public AmmunitionWorld(
            PhysicsScene physicsScene,
            LayerMask hitMask,
            QueryTriggerInteraction triggerInteraction)
        {
            _physicsScene = physicsScene;
            _hitMask = hitMask;
            _triggerInteraction = triggerInteraction;

            _endedView = _endedThisTick.AsReadOnly();
        }

        /// <summary>
        /// 登记一颗新子弹，由当前世界分配 ShotId
        /// 返回 true 只表示登记成功，不代表已经通过起点检测
        /// initialVelocity 由调用方根据枪械初速度和逻辑方向生成
        /// </summary>
        public bool TrySpawn(
            Vector3 startPosition,
            Vector3 initialVelocity,
            Vector3 acceleration,
            float collisionRadius,
            float maxLifetime,
            Transform shooterRoot,
            out ulong shotId)
        {
            shotId = 0;

            // 容量不足或编号耗尽时拒绝登记，不覆盖已有子弹
            if (_active.Count >= MaxActiveCount || _nextShotId == 0)
            {
                return false;
            }

            shotId = _nextShotId;

            // 分配到 ulong 最大值后变为 0，后续登记会被拒绝
            // 不允许重新从 1 开始复用旧编号
            _nextShotId = unchecked(_nextShotId + 1UL);

            AmmunitionState state = new AmmunitionState(
                shotId,
                startPosition,
                initialVelocity,
                acceleration,
                collisionRadius,
                maxLifetime);

            _active.Add(shotId, new ActiveAmmunition(state, shooterRoot));

            return true;
        }

        /// <summary>
        /// 获取活动子弹的状态副本
        /// 修改返回的副本不会修改世界内部状态
        /// </summary>
        public bool TryGetState(
            ulong shotId,
            out AmmunitionState state)
        {
            if (_active.TryGetValue(
                    shotId,
                    out ActiveAmmunition ammunition))
            {
                state = ammunition.State;
                return true;
            }

            state = default;
            return false;
        }

        /// <summary>
        /// 推进所有活动子弹，返回本次结束的子弹结果
        /// 结果列表会在下一次调用或 Clear 时清空
        /// 调用方必须及时消费，不能把列表引用当作历史快照
        /// </summary>
        public IReadOnlyList<AmmunitionEndResult> SimulateTick(float deltaTime)
        {
            _endedThisTick.Clear();

            if (deltaTime <= 0f)
            {
                return _endedView;
            }

            // 遍历期间只修改条目状态，不增删字典成员
            foreach (ActiveAmmunition ammunition in _active.Values)
            {
                bool hasHit = _simulation.SimulateTick(
                    _physicsScene,
                    ref ammunition.State,
                    deltaTime,
                    ammunition.ShooterRoot,
                    _hitMask,
                    _triggerInteraction,
                    out RaycastHit hit);

                if (!ammunition.State.IsAlive)
                {
                    _endedThisTick.Add(
                        new AmmunitionEndResult(
                            ammunition.State,
                            ammunition.ShooterRoot,
                            hasHit,
                            hit));
                }
            }

            // 遍历完成后，再统一移除已结束的子弹
            for (int i = 0; i < _endedThisTick.Count; i++)
            {
                ulong shotId = _endedThisTick[i].State.ShotId;
                _active.Remove(shotId);
            }

            // 外部处理结果时，终态子弹已经不在活动集合中
            return _endedView;
        }

        /// <summary>
        /// 用于整个世界关闭或重置时清理集合
        /// 不产生逐发结束通知，调用方需要同时清理表现对象
        /// 不重置编号，避免同一个世界实例复用旧 ShotId
        /// </summary>
        public void Clear()
        {
            _active.Clear();
            _endedThisTick.Clear();
        }

    }
}

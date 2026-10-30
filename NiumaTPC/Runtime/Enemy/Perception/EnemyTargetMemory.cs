using NiumaTPC.Core.Combat;
using NiumaTPC.Enemy.Core;
using UnityEngine;

namespace NiumaTPC.Enemy.Perception
{
    /// <summary>
    /// 维护敌人最近一次确认的目标与位置
    /// 不负责目标选择、视线检测或移动决策
    /// </summary>
    public sealed class EnemyTargetMemory
    {
        #region Public API

        /// <summary>
        /// 记录当前可见且仍可被选择的目标
        /// 无效目标不会覆盖已有记忆
        /// </summary>
        public void RememberVisibleTarget(EnemyRuntimeData data, IMeleeTarget target, uint serverTick)
        {
            if (data == null || IsInvalidTarget(target))
            {
                return;
            }

            data.CurrentTarget = target;
            data.LastKnownTargetPosition = target.AdsorptionPosition;
            data.LastSeenTargetTick = serverTick;
        }

        /// <summary>
        /// 检查当前目标是否仍处于记忆有效期
        /// 目标死亡、销毁或记忆过期时立即清除
        /// </summary>
        public bool TryGetRememberedTarget(
            EnemyRuntimeData data,
            uint serverTick,
            uint memoryDurationTicks,
            out IMeleeTarget target,
            out Vector3 lastKnownPosition)
        {
            target = null;
            lastKnownPosition = Vector3.zero;

            if (data == null || IsInvalidTarget(data.CurrentTarget))
            {
                ForgetTarget(data);
                return false;
            }

            uint elapsedTicks = unchecked(serverTick - data.LastSeenTargetTick);

            if (elapsedTicks > memoryDurationTicks)
            {
                ForgetTarget(data);
                return false;
            }

            target = data.CurrentTarget;
            lastKnownPosition = data.LastKnownTargetPosition;
            return true;
        }

        /// <summary>
        /// 清除目标引用和最后已知位置
        /// 不修改AI状态、路径或控制意图
        /// </summary>
        public void ForgetTarget(EnemyRuntimeData data)
        {
            if (data == null)
            {
                return;
            }

            data.CurrentTarget = null;
            data.LastKnownTargetPosition = Vector3.zero;
            data.LastSeenTargetTick = 0u;
        }

        #endregion

        #region Validation

        /// <summary>
        /// 检查目标引用是否缺失、已销毁或已经不可选
        /// </summary>
        private static bool IsInvalidTarget(IMeleeTarget target)
        {
            if (target == null)
            {
                return true;
            }

            if (target is Object unityObject && unityObject == null)
            {
                return true;
            }

            return !target.IsTargetable || target.RootTransform == null;
        }

        #endregion
    }
}
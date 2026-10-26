using NiumaTPC.Character.RuntimeData;

namespace NiumaTPC.Character.Simulation
{
    /// <summary>
    /// 将固定 Tick 近战攻击序号转换成一次性表现意图
    /// 不执行吸附、伤害或动画播放
    /// </summary>
    public static class MeleeAttackPresentationBridge
    {
        #region Public API

        public static void Apply(
            PlayerRuntimeData data,
            uint nextSequence,
            uint nextStartTick,
            uint nextCooldownTicksRemaining,
            uint currentTick)
        {
            if (data == null)
            {
                return;
            }

            bool sequenceChanged = data.SimulationMeleeAttackSequence != nextSequence;

            data.SimulationMeleeAttackSequence = nextSequence;

            data.SimulationMeleeAttackStartTick = nextStartTick;

            data.SimulationMeleeAttackCooldownTicksRemaining = nextCooldownTicksRemaining;

            if (!sequenceChanged)
            {
                return;
            }

            /*
             * 首次收到远端角色快照时，序号可能已经不是零
             * 冷却已经结束的旧攻击不能在玩家加入后重新播放
             */
            bool isRecentAttack = nextCooldownTicksRemaining > 0u || nextStartTick == currentTick;

            if (isRecentAttack)
            {
                data.WantsToMeleeAttack = true;
            }
        }

        #endregion
    }
}
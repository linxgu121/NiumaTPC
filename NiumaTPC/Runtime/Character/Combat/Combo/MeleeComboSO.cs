using System.Collections.Generic;
using UnityEngine;

namespace NiumaTPC.Character.Combat
{
    /// <summary>
    /// 一套可以被不同近战武器复用的连招配置
    /// 招式顺序与列表顺序一致
    /// </summary>
    [CreateAssetMenu(fileName = "NewMeleeComboSO",menuName = "NiumaTPC/Combat/MeleeCombo")]
    public class MeleeComboSO : ScriptableObject
    {
        #region Inspector

        [Tooltip("按照执行顺序排列的近战攻击招式 最大支持255段")]
        public List<MeleeComboStep> Steps = new List<MeleeComboStep>();

        #endregion

        #region Public API

        public int StepCount => Steps != null ? Steps.Count : 0;

        /// <summary>
        /// 检查连招列表和招式下标是否能够提供可播放的攻击配置
        /// </summary>
        public bool TryGetStep(
            byte stepIndex,
            out MeleeComboStep step)
        {
            step = null;

            if (Steps == null ||
                Steps.Count == 0 ||
                Steps.Count > byte.MaxValue ||
                stepIndex >= Steps.Count)
            {
                return false;
            }

            step = Steps[stepIndex];

            return step != null && step.AttackAnim != null;
        }

        /// <summary>
        /// 检查指定招式并生成固定 Tick 使用的衔接时间
        /// </summary>
        public bool TryGetStepTiming(
            byte stepIndex,
            float tickDeltaTime,
            out uint nextAttackWindowStartTicks,
            out uint nextAttackWindowEndTicks)
        {
            nextAttackWindowStartTicks = 0u;
            nextAttackWindowEndTicks = 0u;

            if (!TryGetStep(stepIndex, out MeleeComboStep step))
            {
                return false;
            }

            return step.TryCreateTiming(
                tickDeltaTime,
                out nextAttackWindowStartTicks,
                out nextAttackWindowEndTicks);
        }

        #endregion
    }
}
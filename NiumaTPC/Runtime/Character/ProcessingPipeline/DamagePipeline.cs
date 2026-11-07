using System;
using System.Collections.Generic;
using NiumaTPC.Core.Combat;

namespace NiumaTPC.Character.ProcessingPipeline
{
    /// <summary>
    /// 按固定顺序执行目标侧伤害修正器
    /// 不修改生命值或处理死亡
    /// </summary>
    public sealed class DamagePipeline
    {
        #region Internal Types(内部类型)

        private readonly struct ModifierEntry
        {
            public readonly IDamageModifier Modifier;
            public readonly DamageModifierStage Stage;
            public readonly int Priority;
            public readonly ulong RegistrationSequence;

            public ModifierEntry(IDamageModifier modifier, ulong registrationSequence)
            {
                Modifier = modifier;
                Stage = modifier.Stage;
                Priority = modifier.Priority;
                RegistrationSequence = registrationSequence;
            }
        }

        #endregion

        #region Runtime State(运行时状态)

        private readonly List<ModifierEntry> _modifiers = new List<ModifierEntry>(8);
        private ulong _nextRegistrationSequence;

        #endregion

        #region Public API(公共接口)

        public int ModifierCount => _modifiers.Count;

        /// <summary>
        /// 注册目标侧伤害修正器
        /// 同一个实例不能重复注册
        /// </summary>
        public bool Register(IDamageModifier modifier)
        {
            if (modifier == null ||
                !Enum.IsDefined(typeof(DamageModifierStage), modifier.Stage))
            {
                return false;
            }

            for (int i = 0; i < _modifiers.Count; i++)
            {
                if (ReferenceEquals(_modifiers[i].Modifier, modifier)) return false;
            }

            var entry = new ModifierEntry(modifier, _nextRegistrationSequence++);
            int insertIndex = FindInsertIndex(in entry);
            _modifiers.Insert(insertIndex, entry);
            return true;
        }

        /// <summary>
        /// 注销指定修正器实例
        /// 重复注销不会影响其他修正器
        /// </summary>
        public bool Unregister(IDamageModifier modifier)
        {
            if (modifier == null) return false;

            for (int i = 0; i < _modifiers.Count; i++)
            {
                if (!ReferenceEquals(_modifiers[i].Modifier, modifier)) continue;

                _modifiers.RemoveAt(i);
                return true;
            }

            return false;
        }

        /// <summary>
        /// 清除当前管线中的全部修正器
        /// 调用后基础部位修正器也需要重新注册
        /// </summary>
        public void Clear()
        {
            _modifiers.Clear();
            _nextRegistrationSequence = 0ul;
        }

        /// <summary>
        /// 校验原始请求并依次执行所有目标侧伤害修正器
        /// 不合法的修正结果不会进入生命仲裁
        /// </summary>
        public DamageCalculationResult Calculate(in DamageRequest request)
        {
            if (!request.IsValid)
            {
                return new DamageCalculationResult(
                    DamageCalculationStatus.InvalidRequest,
                    in request,
                    request.Amount,
                    0f);
            }

            var context = new DamageCalculationContext(in request);

            for (int i = 0; i < _modifiers.Count; i++)
            {
                _modifiers[i].Modifier.Apply(ref context);

                if (float.IsNaN(context.CurrentDamage) ||
                    float.IsInfinity(context.CurrentDamage) ||
                    context.CurrentDamage < 0f)
                {
                    return new DamageCalculationResult(
                        DamageCalculationStatus.InvalidModifierResult,
                        in request,
                        context.BaseDamage,
                        0f);
                }
            }

            DamageCalculationStatus status = context.CurrentDamage > 0f
                ? DamageCalculationStatus.Applied
                : DamageCalculationStatus.Blocked;

            return new DamageCalculationResult(
                status,
                in request,
                context.BaseDamage,
                context.CurrentDamage);
        }

        #endregion

        #region Ordering(执行顺序)

        private int FindInsertIndex(in ModifierEntry candidate)
        {
            for (int i = 0; i < _modifiers.Count; i++)
            {
                ModifierEntry current = _modifiers[i];
                if (Compare(in candidate, in current) < 0)
                {
                    return i;
                }
            }

            return _modifiers.Count;
        }

        private static int Compare(in ModifierEntry left, in ModifierEntry right)
        {
            int stageComparison = ((byte)left.Stage).CompareTo((byte)right.Stage);
            if (stageComparison != 0) return stageComparison;

            int priorityComparison = left.Priority.CompareTo(right.Priority);
            if (priorityComparison != 0) return priorityComparison;

            return left.RegistrationSequence.CompareTo(right.RegistrationSequence);
        }

        #endregion
    }
}

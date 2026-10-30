using NiumaTPC.Character.Simulation;

namespace NiumaTPC.Character.Combat
{
    /// <summary>
    /// 描述一次近战攻击来源解析后的只读运行配置
    /// 模拟和命中层不需要知道配置来自武器还是怪物能力
    ///
    /// -----------设计公共攻击来源契约的一些说明------------
    /// 一开始近战攻击逻辑是专为玩家武器写的，强依赖了 MeleeWeaponSO 这个配置
    /// 普通怪物没有玩家装备、切换槽位和装备确认流程，因此不能伪装成武器
    /// 后来当我要做怪物和 Boss 时，我发现如果直接复用这套逻辑，就会出现灾难
    /// 于是我剥离了 MeleeAttackDefinition 这个纯数据的只读契约
    /// 无论是玩家的武器，还是怪物的技能，在发起攻击时，都先通过各自的逻辑
    /// 把自己转换为这个 MeleeAttackDefinition，再喂给底层的状态机
    /// SourceRevision用于确认攻击成立前后仍在使用同一个权威攻击来源
    /// 武器使用装备修订号，普通怪物使用稳定的攻击配置编号
    /// Tick、帧号和攻击序号不能作为攻击来源修订号
    ///
    /// ------------如果强行复用会出现以下问题喵！------------
    /// 1.数据源不兼容：玩家读的装备配置，怪物读 AI 行为树/状态机的技能表
    /// 2.网络同步冲突
    /// 3.生命周期不同
    /// 4.吸附参数来源不同
    ///
    /// -------------解答一些问题喵！------------
    /// 那这个框架，在一些简单的怪物等制作上不就会显得太臃肿了，配置起来麻烦？
    /// 会的兄弟，但是可以有解法：适配器（设计模式）做的是归一化
    /// 简单怪物仍然只需配置一个主要攻击
    /// EnemyMeleeAttackSource负责完成来源适配，不会要求怪物进入玩家装备流程
    /// </summary>
    public readonly struct MeleeAttackDefinition
    {
        #region Data(只读数据)

        /// <summary>
        /// 当前攻击使用的轻击或重击通道
        /// </summary>
        public readonly MeleeAttackType AttackType;

        /// <summary>
        /// 当前攻击通道使用的连招配置
        /// </summary>
        public readonly MeleeComboSO Combo;

        /// <summary>
        /// 攻击来源的权威配置修订号
        /// 武器可使用装备修订号，怪物可使用攻击配置编号
        /// </summary>
        public readonly uint SourceRevision;

        /// <summary>
        /// 当前攻击是否允许产生近战吸附
        /// </summary>
        public readonly bool EnableAdsorption;

        /// <summary>
        /// 单次吸附允许角色移动的最大总距离
        /// </summary>
        public readonly float AdsorptionMaximumTravelDistance;

        /// <summary>
        /// 固定Tick吸附模拟使用的运动参数
        /// </summary>
        public readonly MeleeAdsorptionMotionProfile AdsorptionMotionProfile;

        #endregion

        #region Public API(公共接口)

        /// <summary>
        /// 检查攻击通道、连招、来源修订号和吸附参数是否完整
        /// </summary>
        public bool IsValid =>
            AttackType != MeleeAttackType.None &&
            Combo != null &&
            Combo.StepCount > 0 &&
            SourceRevision != 0u &&
            (!EnableAdsorption ||
             IsPositiveFinite(AdsorptionMaximumTravelDistance) &&
             AdsorptionMotionProfile.IsValid);

        public MeleeAttackDefinition(
            MeleeAttackType attackType,
            MeleeComboSO combo,
            uint sourceRevision,
            bool enableAdsorption,
            float adsorptionMaximumTravelDistance,
            in MeleeAdsorptionMotionProfile adsorptionMotionProfile)
        {
            AttackType = attackType;
            Combo = combo;
            SourceRevision = sourceRevision;
            EnableAdsorption = enableAdsorption;
            AdsorptionMaximumTravelDistance = adsorptionMaximumTravelDistance;
            AdsorptionMotionProfile = adsorptionMotionProfile;
        }

        #endregion

        #region Validation(数据校验)

        /// <summary>
        /// 检查距离是否为大于零的有限值
        /// </summary>
        private static bool IsPositiveFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;
        }

        #endregion
    }
}

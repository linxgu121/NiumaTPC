using NiumaTPC.Core.Combat;

namespace NiumaTPC.Character.Combat
{
    /// <summary>
    /// 根据角色的命中部位配置修正目标侧伤害
    /// 未配置Profile时保持原始伤害
    /// </summary>
    public sealed class HitSpotDamageModifier : IDamageModifier
    {
        #region Runtime State(运行时状态)

        private readonly HitSpotDamageProfileSO _profile;

        #endregion

        #region Public API(公共接口)

        public DamageModifierStage Stage => DamageModifierStage.HitSpot;

        public int Priority => 0;

        public HitSpotDamageProfileSO Profile => _profile;

        public HitSpotDamageModifier(HitSpotDamageProfileSO profile)
        {
            _profile = profile;
        }

        /// <summary>
        /// 根据实际命中部位读取倍率并修改当前伤害
        /// </summary>
        public void Apply(ref DamageCalculationContext context)
        {
            if (_profile == null) return;

            float multiplier = _profile.GetMultiplier(context.Request.HitSpot);
            context.CurrentDamage *= multiplier;
        }

        #endregion
    }
}

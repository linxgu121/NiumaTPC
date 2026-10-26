using UnityEngine;

namespace NiumaTPC.Core.Combat
{
    /// <summary>
    /// 角色基础部位伤害倍率配置
    /// 运行时装备和状态不能直接修改该资产
    /// </summary>
    [CreateAssetMenu(
        fileName = "HitSpotDamageProfile",
        menuName = "NiumaTPC/Combat/Hit Spot Damage Profile")]
    public sealed class HitSpotDamageProfileSO : ScriptableObject
    {
        #region Inspector

        [Header("基础部位倍率")]

        [SerializeField]
        [Min(0f)]
        [Tooltip("无法识别具体部位时使用的倍率")]
        private float unknownMultiplier = 1f;

        [SerializeField]
        [Min(0f)]
        [Tooltip("头部基础伤害倍率")]
        private float headMultiplier = 1.5f;

        [SerializeField]
        [Min(0f)]
        [Tooltip("躯干基础伤害倍率")]
        private float torsoMultiplier = 1f;

        [SerializeField]
        [Min(0f)]
        [Tooltip("手臂基础伤害倍率")]
        private float armMultiplier = 0.75f;

        [SerializeField]
        [Min(0f)]
        [Tooltip("腿部基础伤害倍率")]
        private float legMultiplier = 0.8f;

        #endregion

        #region Public API

        /// <summary>
        /// 根据实际命中部位读取角色基础伤害倍率
        /// 未识别的部位使用通用倍率
        /// </summary>
        public float GetMultiplier(HitSpot hitSpot)
        {
            float multiplier = hitSpot switch
            {
                HitSpot.Head => headMultiplier,
                HitSpot.Torso => torsoMultiplier,
                HitSpot.LeftArm => armMultiplier,
                HitSpot.RightArm => armMultiplier,
                HitSpot.LeftLeg => legMultiplier,
                HitSpot.RightLeg => legMultiplier,
                _ => unknownMultiplier
            };

            return Mathf.Max(0f, multiplier);
        }

        #endregion
    }
}
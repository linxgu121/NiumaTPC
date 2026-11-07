using System;

namespace NiumaTPC.Core.Combat
{
    /// <summary>
    /// 描述一次提交给受伤对象的伤害请求
    /// Amount为命中部位与防具处理前的基础伤害
    /// </summary>
    public readonly struct DamageRequest
    {
        /// <summary>
        /// 本次请求的基础伤害
        /// </summary>
        public readonly float Amount;

        /// <summary>
        /// 本次攻击命中的身体部位
        /// </summary>
        public readonly HitSpot HitSpot;

        /// <summary>
        /// 网络攻击者的ObjectId，非网络来源可以为-1
        /// </summary>
        public readonly int SourceObjectId;

        /// <summary>
        /// 攻击者当前攻击序号
        /// </summary>
        public readonly ulong SourceSequence;

        /// <summary>
        /// 攻击成立时使用的攻击来源修订号
        /// </summary>
        public readonly uint SourceRevision;

        /// <summary>
        /// 检查伤害数值与命中部位是否可以安全进入伤害仲裁
        /// </summary>
        public bool IsValid =>
            !float.IsNaN(Amount) &&
            !float.IsInfinity(Amount) &&
            Amount > 0f &&
            Enum.IsDefined(typeof(HitSpot), HitSpot);

        public DamageRequest(
            float amount,
            HitSpot hitSpot = HitSpot.Unknown,
            int sourceObjectId = -1,
            ulong sourceSequence = 0UL,
            uint sourceRevision = 0u)
        {
            Amount = amount;
            HitSpot = hitSpot;
            SourceObjectId = sourceObjectId;
            SourceSequence = sourceSequence;
            SourceRevision = sourceRevision;
        }
    }

    /// <summary>
    /// 接收伤害请求的通用对象接口
    /// </summary>
    public interface IDamageable
    {
        void RequestDamage(in DamageRequest request);
    }
}

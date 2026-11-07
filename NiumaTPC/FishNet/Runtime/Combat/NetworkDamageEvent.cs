using NiumaTPC.Core.Combat;

namespace NiumaTPC.FishNet.Combat
{
    /// <summary>
    /// 客户端播放一次受击表现所需的服务器确认数据
    /// </summary>
    public readonly struct NetworkDamageEvent
    {
        public readonly int SourceObjectId;
        public readonly ulong SourceSequence;
        public readonly uint SourceRevision;
        public readonly HitSpot HitSpot;
        public readonly float AppliedDamage;
        public readonly bool IsLethal;
        public readonly uint ServerTick;

        public NetworkDamageEvent(
            int sourceObjectId,
            ulong sourceSequence,
            uint sourceRevision,
            HitSpot hitSpot,
            float appliedDamage,
            bool isLethal,
            uint serverTick)
        {
            SourceObjectId = sourceObjectId;
            SourceSequence = sourceSequence;
            SourceRevision = sourceRevision;
            HitSpot = hitSpot;
            AppliedDamage = appliedDamage;
            IsLethal = isLethal;
            ServerTick = serverTick;
        }
    }
}

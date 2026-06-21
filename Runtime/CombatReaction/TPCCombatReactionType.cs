using UnityEngine;

namespace NiumaTPC.CombatReaction
{
    public enum TPCCombatReactionType
    {
        [InspectorName("无")]
        None = 0,

        [InspectorName("轻受击")]
        LightHit = 1,

        [InspectorName("重受击")]
        HeavyHit = 2,

        [InspectorName("格挡 / 免伤")]
        Blocked = 3,

        [InspectorName("击倒")]
        Knockdown = 4,

        [InspectorName("死亡")]
        Death = 5
    }
}

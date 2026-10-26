using UnityEngine;

namespace NiumaTPC.Item
{
    /// <summary>
    /// 一颗子弹结束时的本地结果
    /// 保存终态快照，不作为网络消息直接传输
    /// </summary>
    public readonly struct AmmunitionEndResult
    {
        // 结束时的状态快照，包含 ShotId、位置和结束原因
        public readonly AmmunitionState State;

        // 当前仅保存射手过滤根节点，不是网络玩家身份
        public readonly Transform ShooterRoot;

        // 只有为 true 时，Hit 中的命中信息才有效
        public readonly bool HasHit;
        public readonly RaycastHit Hit;

         public AmmunitionEndResult(
            AmmunitionState state,
            Transform shooterRoot,
            bool hasHit,
            RaycastHit hit)
        {
            State = state;
            ShooterRoot = shooterRoot;
            HasHit = hasHit;
            Hit = hasHit ? hit : default;
        }
    }
}

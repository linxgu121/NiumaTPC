namespace NiumaTPC.Core.Faction
{
    /// <summary>
    /// 提供基于有效TeamId的基础队伍关系判断
    /// 不保存队伍数据，也不负责联盟和友军伤害配置
    /// </summary>
    public static class TeamRelationshipUtility
    {
        /// <summary>
        /// 检查队伍接口是否存在并已经分配非零队伍编号
        /// 已销毁的Unity组件与TeamId 0均视为未分配
        /// </summary>
        public static bool IsAssigned(ITeamAffiliation affiliation)
        {
            if (affiliation == null) return false;
            if (affiliation is UnityEngine.Object unityObject && unityObject == null) return false;
            return affiliation.TeamId != 0;
        }

        /// <summary>
        /// 检查双方是否属于不同的有效队伍
        /// 任意一方未分配队伍时按非敌对处理
        /// </summary>
        public static bool IsHostile(ITeamAffiliation first, ITeamAffiliation second)
        {
            return IsAssigned(first) && IsAssigned(second) && first.TeamId != second.TeamId;
        }
    }
}
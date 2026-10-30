namespace NiumaTPC.Core.Faction
{
    /// <summary>
    /// 暴露实体当前所属队伍与阵营关系
    /// 队伍关系由服务器权威规则决定
    /// </summary>
    public interface ITeamAffiliation
    {
        /// <summary>
        /// 当前队伍编号
        /// 0 表示尚未分配队伍
        /// </summary>
        ushort TeamId { get; }
    }
}
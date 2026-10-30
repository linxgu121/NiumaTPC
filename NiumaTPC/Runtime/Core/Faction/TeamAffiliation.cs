using UnityEngine;

namespace NiumaTPC.Core.Faction
{
    /// <summary>
    /// 保存实体当前所属队伍
    /// 不负责判断联盟、仇恨或友军伤害规则
    /// </summary>
    [DisallowMultipleComponent]
    public class TeamAffiliation : MonoBehaviour, ITeamAffiliation
    {
        [SerializeField]
        [Tooltip("当前队伍编号 0表示尚未分配队伍")]
        private ushort teamId;

        /// <summary>
        /// 当前队伍编号
        /// </summary>
        public ushort TeamId => teamId;

        /// <summary>
        /// 由服务器出生流程设置当前队伍
        /// 传入0可以清除队伍归属
        /// </summary>
        public void SetTeamId(ushort value)
        {
            teamId = value;
        }
    }
}

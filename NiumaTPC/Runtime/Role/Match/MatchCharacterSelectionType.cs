namespace NiumaTPC.Role
{
    public enum MatchCharacterSelectionType : byte
    {
        /// <summary>
        /// 正在选角。允许没有候选，也允许修改已有候选。
        /// </summary>
        Selecting = 0,

        /// <summary>
        /// 已确认本局角色
        /// </summary>
        Locked = 1,

        /// <summary>
        /// 本次选角会话已结束，不再接受选择或确认。
        /// </summary>
        Closed = 2
    }
}

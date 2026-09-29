namespace NiumaTPC.Role
{
    /// <summary>
    /// 本地选择上下文提供的角色选择能力
    /// (不负责角色生成，也不代表网络中的所有玩家)
    /// </summary>
    public interface ICharacterSelection
    {
        /// <summary>
        /// 选择的角色ID
        /// </summary>
        ushort? SelectedCharacterId {get;}

        /// <summary>
        /// 选择角色
        /// </summary>
        bool TrySelect(ushort CharacterId, out string error);

        /// <summary>
        /// 清除当前选择
        /// </summary>
        void ClearSelection();
    }
}

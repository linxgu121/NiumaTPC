namespace NiumaTPC.Character.Simulation
{
    /// <summary>
    /// 当前由固定 Tick 模拟器执行的短时动作。
    /// None 表示角色没有执行 Roll 或 Dodge。
    /// </summary>
    public enum CharacterActionType : byte
    {
        None = 0,
        Dodge = 1,
        Roll = 2
    }
}

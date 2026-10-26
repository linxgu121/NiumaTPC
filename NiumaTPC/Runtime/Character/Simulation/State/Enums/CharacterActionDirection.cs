namespace NiumaTPC.Character.Simulation
{
    /// <summary>
    /// Roll、Dodge 开始时锁定的八方向。
    /// 数值按顺时针排列，网络版本不得随意改变顺序。
    /// </summary>
    public enum CharacterActionDirection : byte
    {
        Forward = 0,
        ForwardRight = 1,
        Right = 2,
        BackRight = 3,
        Back = 4,
        BackLeft = 5,
        Left = 6,
        ForwardLeft = 7
    }
}
namespace NiumaTPC.Character.Simulation
{
    /// <summary>
    /// 起步动画和起步运动曲线使用的八方向编号。
    /// 数值顺序按顺时针排列，方便网络存储和配置查询。
    /// </summary>
    public enum CharacterStartDirection : byte
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
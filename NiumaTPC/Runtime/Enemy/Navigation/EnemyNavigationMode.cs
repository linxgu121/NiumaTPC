namespace NiumaTPC.Enemy.Navigation
{
    /// <summary>
    /// 敌人生成追击路径时使用的规划方式
    /// </summary>
    public enum EnemyNavigationMode : byte
    {
        /// <summary>
        /// 忽略场景障碍并直接朝目标移动
        /// 适用于空旷测试场景
        /// </summary>
        Direct = 0,

        /// <summary>
        /// 使用Unity NavMesh计算可通行路径
        /// 角色实际移动仍由TPC固定Tick完成
        /// </summary>
        NavMesh = 1
    }
}
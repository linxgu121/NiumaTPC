using NiumaTPC.Character.RuntimeData;

namespace NiumaTPC.Character.Traversal
{
    /// <summary>
    /// 角色移动模拟与场景环境查询之间的边界
    /// Owner 预测和服务器权威模拟都通过此接口执行相同的翻越检测
    /// </summary>
    public interface ICharacterTraversalProbe
    {
        /// <summary>
        /// 检测指定位置和方向上是否存在有效翻越目标。
        /// </summary>
        bool TryProbeVault(in VaultProbeRequest request, out VaultObstacleInfo obstacleInfo);
    }
}

using UnityEngine;

namespace NiumaTPC.Enemy.Navigation
{
    /// <summary>
    /// 为敌人构建移动路径并提供下一步世界方向
    /// 不直接移动角色或修改Transform
    /// 重建路径 + 沿缓存路径移动
    /// </summary>
    public interface IEnemyPathPlanner
    {
        /// <summary>
        /// 根据当前位置和目标位置重新构建路径
        /// 路径无法成立时清除旧结果
        /// </summary>
        bool TryRebuildPath(Vector3 currentPosition, Vector3 targetPosition);

        /// <summary>
        /// 从已构建路径中取得当前位置对应的下一步水平移动方向
        /// 路径不存在或已经走完时返回false
        /// </summary>
        bool TryGetNextDirection(Vector3 currentPosition, out Vector3 worldDirection);

        /// <summary>
        /// 清除当前路径与内部游标
        /// </summary>
        void Reset();
    }
}
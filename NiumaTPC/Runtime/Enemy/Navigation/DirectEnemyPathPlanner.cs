using UnityEngine;

namespace NiumaTPC.Enemy.Navigation
{
    /// <summary>
    /// 保存目标位置并直接生成朝向目标的水平移动方向
    /// 只适用于没有复杂障碍的测试场景
    /// </summary>
    public sealed class DirectEnemyPathPlanner : IEnemyPathPlanner
    {
        private const float MinimumSqrDistance = 0.000001f;

        #region Runtime State(运行时状态)

        private Vector3 _targetPosition;
        private bool _hasPath;

        #endregion

        #region Public API(公共接口)

        /// <summary>
        /// 检查路径起点和终点是否合法并保存最新目标位置
        /// 非法坐标会清除旧路径
        /// </summary>
        public bool TryRebuildPath(Vector3 currentPosition, Vector3 targetPosition)
        {
            Reset();

            if (!IsFinite(currentPosition) || !IsFinite(targetPosition))
            {
                return false;
            }

            _targetPosition = targetPosition;
            _hasPath = true;
            return true;
        }

        /// <summary>
        /// 计算当前位置直接朝向已保存目标的归一化水平移动方向
        /// 已经与目标水平重合时不生成方向
        /// </summary>
        public bool TryGetNextDirection(Vector3 currentPosition, out Vector3 worldDirection)
        {
            worldDirection = Vector3.zero;

            if (!_hasPath || !IsFinite(currentPosition))
            {
                return false;
            }

            Vector3 displacement = _targetPosition - currentPosition;
            displacement.y = 0f;

            float sqrDistance = displacement.sqrMagnitude;

            if (sqrDistance <= MinimumSqrDistance)
            {
                return false;
            }

            worldDirection = displacement / Mathf.Sqrt(sqrDistance);
            return true;
        }

        public void Reset()
        {
            _targetPosition = Vector3.zero;
            _hasPath = false;
        }

        #endregion

        #region Validation(数据校验)

        /// <summary>
        /// 检查世界坐标的三个分量是否都是有限值
        /// </summary>
        private static bool IsFinite(Vector3 value)
        {
            return !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
                   !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
                   !float.IsNaN(value.z) && !float.IsInfinity(value.z);
        }

        #endregion
    }
}
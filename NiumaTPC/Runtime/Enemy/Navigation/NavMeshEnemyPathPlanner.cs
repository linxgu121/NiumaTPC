using System;
using UnityEngine;
using UnityEngine.AI;

namespace NiumaTPC.Enemy.Navigation
{
    /// <summary>
    /// 使用Unity NavMesh计算路径并沿缓存拐点提供移动方向
    /// 只负责路径规划，不使用NavMeshAgent移动角色
    /// </summary>
    public sealed class NavMeshEnemyPathPlanner : IEnemyPathPlanner
    {
        private const int InitialCornerCapacity = 16;
        private const int MaximumCornerCapacity = 256;
        private const float MinimumSqrDistance = 0.000001f;

        #region Configuration(配置)

        private readonly int _areaMask;
        private readonly float _sampleDistance;
        private readonly float _cornerReachSqrDistance;

        #endregion

        #region Runtime State(运行时状态)

        private readonly NavMeshPath _path = new();
        private Vector3[] _corners = new Vector3[InitialCornerCapacity];

        private int _cornerCount;
        private int _currentCornerIndex;
        private bool _hasPath;

        #endregion

        #region Initialization(初始化)

        public NavMeshEnemyPathPlanner(
            int areaMask,
            float sampleDistance,
            float cornerReachDistance)
        {
            _areaMask = areaMask;
            _sampleDistance = Mathf.Max(0.01f, sampleDistance);

            float safeCornerReachDistance = Mathf.Max(0.01f, cornerReachDistance);
            _cornerReachSqrDistance = safeCornerReachDistance * safeCornerReachDistance;
        }

        #endregion

        #region Public API(公共接口)

        /// <summary>
        /// 将起点和终点投影到NavMesh并构建完整路径
        /// 无法取得完整路径时清除旧结果
        /// </summary>
        public bool TryRebuildPath(Vector3 currentPosition, Vector3 targetPosition)
        {
            Reset();

            if (!IsFinite(currentPosition) || !IsFinite(targetPosition))
            {
                return false;
            }

            if (!NavMesh.SamplePosition(
                    currentPosition,
                    out NavMeshHit startHit,
                    _sampleDistance,
                    _areaMask))
            {
                return false;
            }

            if (!NavMesh.SamplePosition(
                    targetPosition,
                    out NavMeshHit targetHit,
                    _sampleDistance,
                    _areaMask))
            {
                return false;
            }

            if (!NavMesh.CalculatePath(
                    startHit.position,
                    targetHit.position,
                    _areaMask,
                    _path))
            {
                return false;
            }

            // 第一版只接受完整路径，避免敌人在断裂NavMesh边缘持续撞墙
            if (_path.status != NavMeshPathStatus.PathComplete)
            {
                Reset();
                return false;
            }

            _cornerCount = CopyPathCorners();

            if (_cornerCount < 2)
            {
                Reset();
                return false;
            }

            // 第0个拐点通常是路径起点，直接从第1个拐点开始移动
            _currentCornerIndex = 1;
            _hasPath = true;
            return true;
        }

        /// <summary>
        /// 沿缓存路径推进拐点并返回下一步水平移动方向
        /// 到达最终拐点后清除当前路径
        /// </summary>
        public bool TryGetNextDirection(Vector3 currentPosition, out Vector3 worldDirection)
        {
            worldDirection = Vector3.zero;

            if (!_hasPath || !IsFinite(currentPosition))
            {
                return false;
            }

            while (_currentCornerIndex < _cornerCount)
            {
                Vector3 displacement = _corners[_currentCornerIndex] - currentPosition;
                displacement.y = 0f;

                float sqrDistance = displacement.sqrMagnitude;

                if (sqrDistance > _cornerReachSqrDistance)
                {
                    if (sqrDistance <= MinimumSqrDistance)
                    {
                        return false;
                    }

                    worldDirection = displacement / Mathf.Sqrt(sqrDistance);
                    return true;
                }

                _currentCornerIndex++;
            }

            Reset();
            return false;
        }

        public void Reset()
        {
            _path.ClearCorners();
            _cornerCount = 0;
            _currentCornerIndex = -1;
            _hasPath = false;
        }

        #endregion

        #region Path Buffer(路径缓冲)

        private int CopyPathCorners()
        {
            while (true)
            {
                int copiedCount = _path.GetCornersNonAlloc(_corners);

                if (copiedCount < _corners.Length ||
                    _corners.Length >= MaximumCornerCapacity)
                {
                    return copiedCount;
                }

                int newCapacity = Mathf.Min(_corners.Length * 2, MaximumCornerCapacity);

                Array.Resize(ref _corners, newCapacity);
            }
        }

        #endregion

        #region Validation(数据校验)

        /// <summary>
        /// 检查世界坐标三个分量是否都是有限值
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
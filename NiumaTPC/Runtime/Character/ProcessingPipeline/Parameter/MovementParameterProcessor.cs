using NiumaTPC.Character.Config;
using NiumaTPC.Character.RuntimeData;
using NiumaTPC.Character.Simulation;
using UnityEngine;

namespace NiumaTPC.Character.ProcessingPipeline.Parameter
{
    /// <summary>
    /// 动画参数处理器
    /// </summary>
    public class MovementParameterProcessor
    {
        private readonly PlayerRuntimeData _data;
        private readonly PlayerSO _config;
        private readonly Transform _playerTransform;

        // 动画 X Y 参数平滑状态
        private float _currentAnimBlendX;
        private float _xBlendVelocity;
        private float _currentAnimBlendY;
        private float _yBlendVelocity;

        // 记录上一次瞄准状态
        private bool _lastAiming = false;

        // 构造函数 极简注入 仅依赖黑板 配置 与 Transform
        public MovementParameterProcessor(PlayerRuntimeData data, PlayerSO config, Transform playerTransform)
        {
            _data = data;
            _config = config;
            _playerTransform = playerTransform;

            _currentAnimBlendX = 0f;
            _xBlendVelocity = 0f;
            _currentAnimBlendY = 0f;
            _yBlendVelocity = 0f;

        }

        /// <summary>
        /// 更新纯表现参数。
        /// 本地拥有者和远端观察副本都需要执行。
        /// 这些数据只影响动画方向与混合，不决定权威玩法结果。
        /// </summary>
        public void UpdatePresentation()
        {
            UpdateDesiredLocalMoveAngleFromWorldDir();
            UpdateDirectionBlend();
        }

        /// <summary>
        /// 计算移动方向角
        /// 把世界空间期望移动方向，转换成角色局部 Y 轴旋转角度DesiredLocalMoveAngle
        /// 用于 Aim‑Move 分离，角色转向 / 动画的左右偏移角度
        /// </summary>
        private void UpdateDesiredLocalMoveAngleFromWorldDir()
        {
            if (_data.SimulationActionType != CharacterActionType.None)
            {
                /*
                 * Roll/Dodge 的方向已经在动作开始时锁定。
                 * 动作期间不能让新的移动输入覆盖动画方向。
                 */
                return;
            }

            Vector3 worldDir = _data.DesiredWorldMoveDir;
            if (worldDir.sqrMagnitude < 0.0001f)
            {
                _data.DesiredLocalMoveAngle = 0f;
                return;
            }

            Vector3 forward = _playerTransform.forward;
            worldDir.y = 0f;
            forward.y = 0f;

            if (worldDir.sqrMagnitude < 0.0001f || forward.sqrMagnitude < 0.0001f)
            {
                _data.DesiredLocalMoveAngle = 0f;
                return;
            }

            worldDir.Normalize();
            forward.Normalize();
            float angle = Vector3.SignedAngle(forward, worldDir, Vector3.up);
            _data.DesiredLocalMoveAngle = angle;
        }
        
        /// <summary>
        /// 动画XY方向平滑混合
        /// </summary>
        private void UpdateDirectionBlend()
        {
            Vector2 input = _data.MoveInput;
            Vector2 circle = input.sqrMagnitude < 0.0001f ? Vector2.zero : input.normalized;

            bool aimingNow = _data.IsAiming;
            if (aimingNow != _lastAiming)
            {
                _xBlendVelocity = 0f;
                _yBlendVelocity = 0f;
            }
            _lastAiming = aimingNow;

            float xSmoothTime, ySmoothTime;
            if (_config.Aiming == null)
            {
                xSmoothTime = _config.Core.XAnimBlendSmoothTime;
                ySmoothTime = _config.Core.YAnimBlendSmoothTime;
            }
            else
            {
                xSmoothTime = Mathf.Max(0.0001f, aimingNow ? _config.Aiming.AimXAnimBlendSmoothTime : _config.Core.XAnimBlendSmoothTime);
                ySmoothTime = Mathf.Max(0.0001f, aimingNow ? _config.Aiming.AimYAnimBlendSmoothTime : _config.Core.YAnimBlendSmoothTime);
            }

            _currentAnimBlendX = Mathf.SmoothDamp(_currentAnimBlendX, circle.x, ref _xBlendVelocity, xSmoothTime, Mathf.Infinity, Time.deltaTime);
            _data.CurrentAnimBlendX = _currentAnimBlendX;

            _currentAnimBlendY = Mathf.SmoothDamp(_currentAnimBlendY, circle.y, ref _yBlendVelocity, ySmoothTime, Mathf.Infinity, Time.deltaTime);
            _data.CurrentAnimBlendY = _currentAnimBlendY;
        }

    }
}

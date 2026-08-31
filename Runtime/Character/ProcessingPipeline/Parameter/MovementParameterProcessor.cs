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

        // 下落距离计算状态 追踪本次空中过程的最高点
        private float _apexY;
        private bool _wasGroundedLastFrame;

        // 下落意图计算状态 累积空中时间判断是否应该进入下落动画
        private float _airborneTime;

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

            _apexY = _playerTransform.position.y;
            _wasGroundedLastFrame = _data.IsGrounded;
            _airborneTime = 0f;
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

        public void UpdateGameplay()
        {
            UpdateFallHeight();
            UpdateFallIntent();
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

        /// <summary>
        /// 下落高度、落地/离地标记
        /// </summary>
        private void UpdateFallHeight()
        {
            bool isGrounded = _data.IsGrounded;
            float currentY = _playerTransform.position.y;

            bool justLanded = !_wasGroundedLastFrame && isGrounded;
            bool justLeftGround = _wasGroundedLastFrame && !isGrounded;

            _data.JustLanded = justLanded;
            _data.JustLeftGround = justLeftGround;

            if (isGrounded)
            {
                _apexY = currentY;
                if (!justLanded)
                {
                    _data.FallHeightLevel = 0;
                }
            }
            else
            {
                if (justLeftGround) _apexY = currentY;
                if (currentY > _apexY) _apexY = currentY;

                float fallHeight = Mathf.Max(0f, _apexY - currentY);
                CalculateFallHeightLevel(fallHeight);
            }

            _wasGroundedLastFrame = isGrounded;
        }

        /// <summary>
        /// 空中时间，判断是否播放下落动画
        /// </summary>
        private void UpdateFallIntent()
        {
            bool isGrounded = _data.IsGrounded;

            if (isGrounded)
            {
                _airborneTime = 0f;
                _data.WantsToFall = false;
            }
            else
            {
                _airborneTime += Time.deltaTime;
                float fallTimeThreshold = _config.LocomotionAnims.AirborneTimeThresholdForFall;
                _data.WantsToFall = _airborneTime >= fallTimeThreshold;
            }
        }

        private void CalculateFallHeightLevel(float height)
        {
            if (height < _config.JumpAndLanding.LandHeight_Level1) _data.FallHeightLevel = 0;
            else if (height < _config.JumpAndLanding.LandHeight_Level2) _data.FallHeightLevel = 1;
            else if (height < _config.JumpAndLanding.LandHeight_Level3) _data.FallHeightLevel = 2;
            else if (height < _config.JumpAndLanding.LandHeight_Level4) _data.FallHeightLevel = 3;
            else _data.FallHeightLevel = 4;
        }
    }
}

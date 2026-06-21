using System.Collections;
using System.Collections.Generic;
using NiumaTPC.Character.Config;
using NiumaTPC.Character.Motion.MotionEnums;
using NiumaTPC.Character.RuntimeData;
using UnityEngine;

namespace NiumaTPC.Character.Core.Driver
{
    /*CalculateInputDrivenVelocity()  （主分支）
    ├── CalculateFreeLookVelocity()    [自由视角模式]
    │   ├── 1. 读取 DesiredWorldMoveDir（输入方向）
    │   ├── 2. 通过 SmoothDampAngle 平滑角色朝向（CurrentYaw）
    │   └── 3. 计算平滑速度（SmoothSpeed）
    │
    └── CalculateAimVelocity()         [瞄准模式]
        ├── 1. 角色朝向 AuthorityYaw（权威朝向）
        ├── 2. 将摇杆输入投影到 forward/right
        ├── 3. 检测反向输入（直接清零速度平滑状态）
        └── 4. 计算平滑速度

    CalculateClipDrivenVelocity()   （动画驱动分支）
    ├── 曲线段阶段 → CalculateCurveVelocity()
    │   ├── 1. 读取旋转曲线计算转向角度
    │   ├── 2. 读取速度曲线
    │   └── 3. 使用动画目标方向生成世界速度
    │
    └── 混合段阶段（Mixed） → 切回 CalculateInputDrivenVelocity()
        ├── 1. 对齐速度/旋转状态
        └── 2. 恢复输入驱动*/
    
    /// <summary>
    /// 角色运动的核心驱动器 负责将输入、动画曲线、物理参数
    /// 转换为实际的 CharacterController.Move()调用 驱动角色在场景中的实际位移
    /// </summary>
    public class MotionDriver
    {
        #region 依赖组件
        private readonly NiumaCharacterController _player;
        private readonly CharacterController _cc;
        private readonly PlayerRuntimeData _data;
        private readonly PlayerSO _config;
        private readonly Transform _transform;
        #endregion

        #region 上下文缓存

        /// <summary>
        /// 缓存移动用的临时数据
        /// </summary>
        private struct LocomotionCtx
        {
            public bool WasAiming;

            // 平滑速度(标量)
            public float SmoothSpeed;
            public float SpeedVelocity;

            // 用于检测瞄准形态下反向切输入，避免 SmoothDamp 造成“拖拽”
            public Vector3 LastAimMoveDir;

            // 重置速度相关数据
            public void ResetSpeed()
            {
                SmoothSpeed = 0f;
                SpeedVelocity = 0f;
                LastAimMoveDir = Vector3.zero;
            }
        }

        /// <summary>
        /// 缓存曲线驱动运动时的临时数据
        /// </summary>
        private struct CurveCtx
        {
            // 上一帧的旋转角度（用于平滑旋转，防止跳变）
            public float LastAngle;

            // 是否初始化过（防止第一帧数据异常）
            public bool IsInitialized;

            // 上一次的运动驱动类型（Input/Curve/Mixed，用于状态切换判断）
            public MotionType? LastMotionType;

            // 在混合驱动模式下，是否已经完成朝向对齐
            public bool DidAlignOnMixed;

            // 重置数据
            public void Reset()
            {
                LastAngle = 0f;
                IsInitialized = false;
            }
        }

        /// <summary>
        /// 技能冲刺/强制移动/位置矫正专用缓存
        /// </summary>
        private struct WarpCtx
        {
            //当前正在执行的 扭曲运动数据（配置好的技能位移曲线、速度、偏移等）
            public WarpedMotionData Data;
            //位移运动经过的所有目标位置点
            public Vector3[] Targets;
            //当前运动到 第几个路径点
            public int CurrentIndex;
            //当前这段路径（Segment）的开始时间
            public float SegmentStartTime;
            //当前这段路径的起始位置
            public Vector3 SegmentStartPosition;
            // 补偿速度:用于位置误差修正、防卡、防穿模
            public Vector3 CompensationVel;
            //是否正在进行扭曲运动
            public bool IsActive => Data != null;
            //清空所有数据，结束位移
            public void Clear()
            {
                Data = null;
                Targets = null;
                CompensationVel = Vector3.zero;
                CurrentIndex = 0;
                SegmentStartTime = 0f;
                SegmentStartPosition = Vector3.zero;
            }
        }

        private LocomotionCtx _loco;
        private CurveCtx _curve;
        private WarpCtx _warp;

        // 单帧重力缓存：避免同帧多处调用重复积分 VerticalVelocity
        private int _gravityFrame = -1;
        private Vector3 _cachedGravity;

        #endregion


        public MotionDriver(NiumaCharacterController player)
        {
            _player = player;
            _cc = player.CharacterController;
            _data = player.RuntimeData;
            _config = player.Config;
            _transform = player.transform;

            _loco.WasAiming = _data.IsAiming;
        }

        #region 外部调用接口API
        
        /// <summary>
        /// 仅更新重力
        /// </summary>
        public void UpdateGravityOnly()
        {
            Vector3 vv = GetGravityThisFrame();
            _cc.Move(vv * Time.deltaTime);
            _data.CurrentSpeed = _cc.velocity.magnitude;
        }

        /// <summary>
        /// 根据动画数据自动选择 曲线驱动/输入驱动
        /// </summary>
        public void UpdateMotion(MotionClipData clipData, float stateTime, bool applyGravity = true)
        {
            HandleAimModeTransitionIfNeeded();
            AutoHandleCurveDrivenEnter(clipData, stateTime);

            Vector3 hv = clipData == null
                ? CalculateInputDrivenVelocity(1f)
                : CalculateClipDrivenVelocity(clipData, stateTime);

            ExecuteMovement(hv, applyGravity);
        }

        /// <summary>
        /// 纯输入驱动的移动（走/跑/待机)
        /// </summary>
        public void UpdateLocomotionFromInput(float speedMult = 1f)
        {
            HandleAimModeTransitionIfNeeded();
            ExecuteMovement(CalculateInputDrivenVelocity(speedMult));
        }

        /// <summary>
        /// 无输入原地不动
        /// </summary>
        public void UpdateMotion() => ExecuteMovement(Vector3.zero);

        /// <summary>
        /// 中断动画曲线驱动的运动（技能/闪避强制停止）
        /// </summary>
        public void InterruptClipDrivenMotion()
        {
            _curve.LastMotionType = null;
            _curve.DidAlignOnMixed = false;
            _curve.Reset();
        }

        #endregion
        
        #region 位移扭曲API(技能冲刺/强制位移)

        /// <summary>
        /// 初始化扭曲运动（带目标点数组）
        /// </summary>
        public void InitializeWarpData(WarpedMotionData data, Vector3[] targets)
        {
            if (data == null || data.WarpPoints == null || data.WarpPoints.Count == 0 ||
                targets == null || targets.Length != data.WarpPoints.Count)
            {
                Debug.LogError("运动扭曲数据初始化失败 参数不匹配");
                return;
            }

            _warp.Data = data;
            _warp.Targets = new Vector3[targets.Length];

            // 目标偏移：按角色根空间转换到世界。
            for (int i = 0; i < targets.Length; i++)
            {
                Vector3 worldOffset = _transform.TransformDirection(_warp.Data.WarpPoints[i].TargetPositionOffset);
                _warp.Targets[i] = targets[i] + worldOffset;
            }

            _warp.CurrentIndex = 0;
            _warp.SegmentStartTime = 0f;
            _warp.SegmentStartPosition = _transform.position;
            RecalculateWarpCompensation();
        }

        /// <summary>
        /// 初始化扭曲运动（自动生成路径）
        /// </summary>
         public void InitializeWarpData(WarpedMotionData data)
        {
            if (data?.WarpPoints == null || data.WarpPoints.Count == 0) return;

            Vector3[] targets = new Vector3[data.WarpPoints.Count];
            for (int i = 0; i < data.WarpPoints.Count; i++)
            {
                targets[i] = _transform.position + _transform.TransformVector(data.WarpPoints[i].BakedLocalOffset);
            }

            InitializeWarpData(data, targets);
        }
        
        /// <summary>
        /// 更新扭曲运动（按动画进度执行位移）
        /// </summary>
         public void UpdateWarpMotion(float normalizedTime)
        {
            if (!_warp.IsActive) return;

            // warp 期间不使用普通平滑(直接读当前水平速度只是为了保持数据一致)
            Vector3 v = _cc.velocity;
            _loco.SmoothSpeed = new Vector3(v.x, 0f, v.z).magnitude;
            _loco.SpeedVelocity = 0f;

            CheckAndAdvanceWarpSegment(normalizedTime);

            // 本地速度曲线 -> 世界
            Vector3 localVel = new Vector3(
                _warp.Data.LocalVelocityX.Evaluate(normalizedTime),
                _warp.Data.LocalVelocityY.Evaluate(normalizedTime),
                _warp.Data.LocalVelocityZ.Evaluate(normalizedTime)
            );

            Vector3 finalVelocity = _transform.TransformDirection(localVel) + _warp.CompensationVel;

            if (_warp.Data.ApplyGravity)
            {
                finalVelocity += GetGravityThisFrame();
            }
            else
            {
                _data.IsGrounded = _cc.isGrounded;
            }

            float rotVelY = _warp.Data.LocalRotationY.Evaluate(normalizedTime);

            _cc.Move(finalVelocity * Time.deltaTime);
            if (Mathf.Abs(rotVelY) > 0.0001f)
                _transform.Rotate(0f, rotVelY * Time.deltaTime, 0f, Space.World);

            _data.CurrentSpeed = _cc.velocity.magnitude;
        }

        /// <summary>
        /// 清空扭曲运动数据
        /// </summary>
        public void ClearWarpData() => _warp.Clear();

        #endregion
        
        #region 核心移动逻辑

        /// <summary>
        /// 执行最终移动（水平速度 + 重力）
        /// </summary>
        private void ExecuteMovement(Vector3 horizontalVelocity, bool applyGravity = true)
        {
            Vector3 vv = applyGravity ? GetGravityThisFrame() : Vector3.zero;
            _cc.Move((horizontalVelocity + vv) * Time.deltaTime);
            _data.CurrentSpeed = _cc.velocity.magnitude;
        }

        /// <summary>
        /// 计算动画曲线驱动的速度（技能/闪避）
        /// </summary>
        private Vector3 CalculateClipDrivenVelocity(MotionClipData clipData, float stateTime)
        {
            bool isCurvePhase = clipData.Type == MotionType.CurveDriven ||
                               (clipData.Type == MotionType.Mixed && stateTime < clipData.RotationFinishedTime);

            if (isCurvePhase)
            {
                _curve.DidAlignOnMixed = false;
                return CalculateCurveVelocity(clipData, stateTime);
            }

            // Mixed 从曲线段切到输入段：只对齐一次
            if (clipData.Type == MotionType.Mixed && !_curve.DidAlignOnMixed)
            {
                AlignAndResetForInputTransition();
                _curve.DidAlignOnMixed = true;
            }

            return CalculateInputDrivenVelocity(1f);
        }

        /// <summary>
        /// 计算输入驱动速度（自动判断瞄准/自由视角）
        /// </summary>
        private Vector3 CalculateInputDrivenVelocity(float speedMult)
        {
            return _data.IsAiming
                ? CalculateAimVelocity(speedMult)
                : CalculateFreeLookVelocity(speedMult);
        }
        #endregion

        #region 移动模式（自由/瞄准/动画曲线）

        /// <summary>
        /// 自由视角移动（非瞄准）
        /// </summary>
        private Vector3 CalculateFreeLookVelocity(float speedMult)
        {
            Vector3 moveDir = _data.DesiredWorldMoveDir;

            if (moveDir.sqrMagnitude < 0.0001f)
            {
                // 避免 eulerAngles 多次读取，空输入时 CurrentYaw 维持最新值即可
                _loco.SmoothSpeed = 0f;
                return Vector3.zero;
            }

            float targetYaw = Mathf.Atan2(moveDir.x, moveDir.z) * Mathf.Rad2Deg;
            ApplySmoothYaw(targetYaw, _config.Core.RotationSmoothTime);

            return CalculateSmoothedVelocity(moveDir, isAiming: false, speedMult);
        }
        
        /// <summary>
        /// 瞄准模式移动
        /// </summary>
        private Vector3 CalculateAimVelocity(float speedMult)
        {
            // 瞄准模式：朝权威 yaw 转向
            ApplySmoothYaw(_data.AuthorityYaw, _config.Aiming.AimRotationSmoothTime);

            Vector2 input = _data.MoveInput;
            if (input.sqrMagnitude < 0.001f)
            {
                _loco.ResetSpeed();
                return Vector3.zero;
            }

            // 平面 forward/right 投影
            Vector3 f = _transform.forward;
            f.y = 0f;
            float fMag = f.magnitude;
            if (fMag > 0.0001f) f /= fMag;

            Vector3 r = _transform.right;
            r.y = 0f;
            float rMag = r.magnitude;
            if (rMag > 0.0001f) r /= rMag;

            Vector3 move = (r * input.x + f * input.y);
            if (move.sqrMagnitude > 0.0001f) move.Normalize();

            // 反向切输入，直接清零 SmoothDamp 状态
            if (_loco.LastAimMoveDir.sqrMagnitude > 0.1f && Vector3.Dot(move, _loco.LastAimMoveDir) < 0f)
            {
                _loco.SmoothSpeed = 0f;
                _loco.SpeedVelocity = 0f;
            }

            _loco.LastAimMoveDir = move;
            return CalculateSmoothedVelocity(move, isAiming: true, speedMult);
        }

        /// <summary>
        /// 动画曲线驱动位移
        /// </summary>
        private Vector3 CalculateCurveVelocity(MotionClipData data, float time)
        {
            float t = time * data.PlaybackSpeed;

            // 旋转曲线：用 deltaAngle 推进
            float curveAngle = data.RotationCurve.Evaluate(t);
            if (!_curve.IsInitialized)
            {
                _curve.LastAngle = curveAngle;
                _curve.IsInitialized = true;
            }

            float deltaAngle = curveAngle - _curve.LastAngle;
            _curve.LastAngle = curveAngle;

            if (Mathf.Abs(deltaAngle) > 0.0001f)
                _transform.Rotate(0f, deltaAngle, 0f, Space.World);

            // 动画驱动阶段：仍同步 CurrentYaw，供其他系统读取
            _data.CurrentYaw = _transform.eulerAngles.y;

            float speed = data.SpeedCurve.Evaluate(t);
            Vector3 localDir = data.TargetLocalDirection;

            if (localDir.sqrMagnitude > 0.0001f)
            {
                // 仅平面转换
                Vector3 worldDir = _transform.TransformDirection(localDir.SetY(0f));
                worldDir.y = 0f;
                if (worldDir.sqrMagnitude > 0.0001f) worldDir.Normalize();
                return worldDir * speed;
            }

            return _transform.forward * speed;
        }
        #endregion

        #region 辅助方法

        /// <summary>
        /// 平滑旋转角色朝向
        /// </summary>
        private void ApplySmoothYaw(float targetYaw, float smoothTime)
        {
            // 用 CurrentYaw 做权威 yaw
            float currentYaw = _data.CurrentYaw;
            if (currentYaw == 0f)
            {
                // 首帧或外部未初始化时，兜底读一次
                currentYaw = _transform.eulerAngles.y;
            }

            float smoothed = Mathf.SmoothDampAngle(currentYaw, targetYaw, ref _data.RotationVelocity, smoothTime);
            _transform.rotation = Quaternion.Euler(0f, smoothed, 0f);
            _data.CurrentYaw = smoothed;
        }

        /// <summary>
        /// 计算平滑后的速度（防止顿挫）
        /// </summary>
        private Vector3 CalculateSmoothedVelocity(Vector3 moveDir, bool isAiming, float speedMult)
        {
            float baseSpeed = GetBaseSpeed(_data.CurrentLocomotionState, isAiming);
            if (!_data.IsGrounded) baseSpeed *= _config.Core.AirControl;

            float targetSpeed = baseSpeed * speedMult;
            _loco.SmoothSpeed = Mathf.SmoothDamp(_loco.SmoothSpeed, targetSpeed, ref _loco.SpeedVelocity, _config.Core.MoveSpeedSmoothTime);
            return moveDir * _loco.SmoothSpeed;
        }

        /// <summary>
        /// 获取当前状态基础速度
        /// </summary>
        private float GetBaseSpeed(LocomotionState state, bool isAiming) => state switch
        {
            LocomotionState.Walk => isAiming ? _config.Aiming.AimWalkSpeed : _config.Core.WalkSpeed,
            LocomotionState.Jog => isAiming ? _config.Aiming.AimJogSpeed : _config.Core.JogSpeed,
            LocomotionState.Sprint => isAiming ? _config.Aiming.AimSprintSpeed : _config.Core.SprintSpeed,
            _ => 0f
        };

        /// <summary>
        /// 获取本帧重力
        /// </summary>
        private Vector3 GetGravityThisFrame()
        {
            int frame = Time.frameCount;
            if (_gravityFrame == frame) return _cachedGravity;
            _gravityFrame = frame;

            _data.IsGrounded = _cc.isGrounded;

            // grounded 且向下速度为负：回弹到小负值/贴地力
            if (_data.IsGrounded && _data.VerticalVelocity < 0f)
                _data.VerticalVelocity = _config.Core.ReboundForce;
            else
                _data.VerticalVelocity += _config.Core.Gravity * Time.deltaTime;

            _cachedGravity = new Vector3(0f, _data.VerticalVelocity, 0f);
            return _cachedGravity;
        }

        /// <summary>
        /// 瞄准/非瞄准切换时重置平滑数据
        /// </summary>
        private void HandleAimModeTransitionIfNeeded()
        {
            if (_data.IsAiming == _loco.WasAiming) return;

            // 形态切换：清理旋转与速度平滑状态
            _data.RotationVelocity = 0f;
            _loco.LastAimMoveDir = Vector3.zero;
            _loco.SpeedVelocity = 0f;
            _loco.WasAiming = _data.IsAiming;
        }

        /// <summary>
        /// 进入动画曲线驱动时自动初始化
        /// </summary>
        private void AutoHandleCurveDrivenEnter(MotionClipData clipData, float stateTime)
        {
            MotionType? current = clipData?.Type;
            bool isCurvePhase = current == MotionType.CurveDriven ||
                                (current == MotionType.Mixed && stateTime < clipData?.RotationFinishedTime);

            bool wasCurveLogic = _curve.LastMotionType == MotionType.CurveDriven ||
                                 _curve.LastMotionType == MotionType.Mixed;

            // 进入曲线段：重置曲线内部状态
            if (isCurvePhase && (!wasCurveLogic || !_curve.IsInitialized))
            {
                _curve.Reset();
                _data.RotationVelocity = 0f;
                _curve.DidAlignOnMixed = false;
            }

            _curve.LastMotionType = current;
        }

        /// <summary>
        /// 混合模式切回输入时对齐朝向并重置
        /// </summary>
        private void AlignAndResetForInputTransition()
        {
            // Mixed 切换输入段：清理旋转速度 避免 SmoothDampAngle 残留
            _data.RotationVelocity = 0f;
            _data.CurrentYaw = _transform.eulerAngles.y;
            _curve.IsInitialized = false;
        }

        #endregion

        #region 位移扭曲辅助方法

        /// <summary>
        /// 检查并推进到下一段扭曲路径
        /// </summary>
        private void CheckAndAdvanceWarpSegment(float normalizedTime)
        {
            if (_warp.CurrentIndex >= _warp.Data.WarpPoints.Count) return;

            float targetTime = _warp.Data.WarpPoints[_warp.CurrentIndex].NormalizedTime;
            if (normalizedTime >= targetTime)
            {
                _warp.CurrentIndex++;
                _warp.SegmentStartTime = targetTime;
                _warp.SegmentStartPosition = _transform.position;
                RecalculateWarpCompensation();
            }
        }

        /// <summary>
        /// 重新计算位移补偿速度（修正动画与真实位置偏差）
        /// </summary>
        private void RecalculateWarpCompensation()
        {
            if (_warp.CurrentIndex >= _warp.Data.WarpPoints.Count)
            {
                _warp.CompensationVel = Vector3.zero;
                return;
            }

            var warpPoint = _warp.Data.WarpPoints[_warp.CurrentIndex];
            float segmentSeconds = (warpPoint.NormalizedTime - _warp.SegmentStartTime) * _warp.Data.BakedDuration;

            if (segmentSeconds < 0.01f)
            {
                _warp.CompensationVel = Vector3.zero;
                return;
            }

            Vector3 realDelta = _warp.Targets[_warp.CurrentIndex] - _warp.SegmentStartPosition;
            Vector3 animDelta = _transform.TransformVector(warpPoint.BakedLocalOffset);

            _warp.CompensationVel = (realDelta - animDelta) / segmentSeconds;
        }

        #endregion
    }

    

    /// <summary>
    /// 扩展方法：快速设置 Vector3.y
    /// </summary>
     public static class Vector3Extensions
        {
            public static Vector3 SetY(this Vector3 vector, float y)
            {
               vector.y = y;
               return vector; 
            }
        }
}

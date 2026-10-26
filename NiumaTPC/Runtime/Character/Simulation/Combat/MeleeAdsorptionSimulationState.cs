using UnityEngine;

namespace NiumaTPC.Character.Simulation
{
    /// <summary>
    /// 正在固定 Tick 模拟中执行的近战吸附状态
    /// 该状态会随 CharacterSimulationState 参与预测与 Reconcile
    /// </summary>
    public struct MeleeAdsorptionSimulationState
    {
        /// <summary>
        /// 当前是否正在执行吸附
        /// </summary>
        public bool IsActive;

        /// <summary>
        /// 吸附开始时冻结的目标与最大位移数据
        /// </summary>
        public MeleeAdsorptionSimulationRequest Request;

        /// <summary>
        /// 吸附开始时冻结的运动参数
        /// 攻击过程中切换装备不能改变本次吸附
        /// </summary>
        public MeleeAdsorptionMotionProfile MotionProfile;

        /// <summary>
        /// 已经完成的吸附模拟 Tick 数
        /// </summary>
        public uint ElapsedTicks;

        /// <summary>
        /// 吸附期间累计产生的实际水平位移
        /// 用于限制绕过障碍或碰撞滑动产生的总移动距离
        /// </summary>
        public float AccumulatedTravelDistance;

        /// <summary>
        /// 检查活动状态能否继续参与预测和服务器模拟
        /// 未激活的默认状态视为合法空状态
        /// </summary>
        public bool IsValid
        {
            get
            {
                if (!IsActive)
                {
                    return true;
                }

                return
                    Request.IsValid &&
                    MotionProfile.IsValid &&
                    ElapsedTicks <
                    MotionProfile.DurationTicks &&
                    IsNonNegativeFinite(
                        AccumulatedTravelDistance) &&
                    AccumulatedTravelDistance <=
                    Request.MaximumTravelDistance;
            }
        }

        /// <summary>
        /// 使用已经验证的请求和运动参数创建新的活动状态
        /// 无效数据不能进入预测或服务器权威模拟
        /// </summary>
        public static bool TryBegin(
            in MeleeAdsorptionSimulationRequest request,
            in MeleeAdsorptionMotionProfile motionProfile,
            out MeleeAdsorptionSimulationState state)
        {
            state = default;

            if (!request.IsValid ||
                !motionProfile.IsValid)
            {
                return false;
            }

            state = new MeleeAdsorptionSimulationState
            {
                IsActive = true,
                Request = request,
                MotionProfile = motionProfile,
                ElapsedTicks = 0u,
                AccumulatedTravelDistance = 0f
            };

            return true;
        }

        /// <summary>
        /// 结束吸附并清除目标快照与累计运动数据
        /// </summary>
        public void Stop()
        {
            this = default;
        }

        /// <summary>
        /// 检查累计距离是否为非负有限值
        /// </summary>
        private static bool IsNonNegativeFinite(float value)
        {
            return
                !float.IsNaN(value) &&
                !float.IsInfinity(value) &&
                value >= 0f;
        }
    }
}
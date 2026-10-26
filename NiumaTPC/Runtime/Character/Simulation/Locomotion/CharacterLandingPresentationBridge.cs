using NiumaTPC.Character.Config.PlayerSOModules;
using NiumaTPC.Character.RuntimeData;

namespace NiumaTPC.Character.Simulation
{
    /// <summary>
    /// 将模拟层的落地结果转换成表现层的一次性标记
    /// 只负责事件去重和落差等级转换，不计算位移或播放动画
    /// </summary>
    public sealed class CharacterLandingPresentationBridge
    {
        #region Runtime State

        private bool _hasBaseline;

        // 表现层已经见过的最新落地序号
        // 不随模拟校正回退，避免重放同一次落地表现
        private uint _lastLandingSequence;

        #endregion

        #region Public API

        /// <summary>
        /// 清除表现事件历史，供网络停止或对象重新使用时调用
        /// 黑板中的待消费标记由调用方一起清理
        /// </summary>
        public void Reset()
        {
            _hasBaseline = false;
            _lastLandingSequence = 0u;
        }

        /// <summary>
        /// 建立当前落地序号的基线，不生成落地表现
        /// 本地模拟初始化及观察者首次接收快照时使用
        /// </summary>
        public void SetBaseline(uint landingSequence)
        {
            _hasBaseline = true;
            _lastLandingSequence = landingSequence;
        }

        /// <summary>
        /// 将新的落地结果写入黑板
        /// 重复快照和校正回退不能重复产生落地标记
        /// </summary>
        public void Apply(
            PlayerRuntimeData data,
            JumpSO config,
            bool isGrounded,
            uint landingSequence,
            float landingFallHeight)
        {
            if (data == null || config == null)
            {
                return;
            }

            // 首份历史快照只建立基线，不补播加入前发生的落地
            if (!_hasBaseline)
            {
                SetBaseline(landingSequence);
                return;
            }

            if (!IsNewerLandingSequence(landingSequence,_lastLandingSequence))
            {
                return;
            }

            _lastLandingSequence = landingSequence;

            // 快照到达时角色已经再次离地，不在空中补播上次落地
            if (!isGrounded)
            {
                return;
            }

            data.FallHeightLevel = ResolveFallHeightLevel(config, landingFallHeight);

            // 这里只置为 true，不让后续普通 Tick 清掉尚未消费的事件
            data.JustLanded = true;
        }

        #endregion

        #region Landing Conversion

        /// <summary>
        /// 检查是否为更新的落地序号，拒绝零值、重复值和回退值
        /// 支持 uint 回绕，比较跨度须小于半个序号空间
        /// </summary>
        private static bool IsNewerLandingSequence(uint candidate,uint previous)
        {
            if (candidate == 0u)
            {
                return false;
            }

            // 零是尚未落地的初始化标记，不参与正常序号比较
            if (previous == 0u)
            {
                return true;
            }

            return unchecked((int)(candidate - previous)) > 0;
        }

        /// <summary>
        /// 沿用现有四个高度阈值，将落差映射为落地表现等级
        /// 等级零不触发当前落地拦截器的缓冲动画
        /// </summary>
        private static int ResolveFallHeightLevel(JumpSO config,float height)
        {
            if (height < config.LandHeight_Level1)
                return 0;

            if (height < config.LandHeight_Level2)
                return 1;

            if (height < config.LandHeight_Level3)
                return 2;

            if (height < config.LandHeight_Level4)
                return 3;

            return 4;
        }

        #endregion
    }
}
using System;

namespace NiumaTPC.Character.Core.Animation
{

    /// <summary>
    /// 动画播放代理选项
    /// 集中管理所有播放层的额外配置
    /// </summary>
    [Serializable]
    public struct AnimPlayOptions
    {
        /// <summary>
        /// 目标播放层级 默认为0（基本层）
        /// </summary>
        public int Layer;

        /// <summary>
        /// 过渡时间 如果为 -1 则使用动画/Transition自身在 Inspector 中配置的默认 Fade
        /// 仅当值 >= 0 时才会被应用
        /// </summary>
        public float FadeDuration;
        /// <summary>
        /// 播放速度
        /// 如果为 -1 则表示不显式设置速度
        /// 仅当值 > 0时才会被应用
        /// </summary>
        public float Speed;
        /// <summary>
        /// 指定起始的归一化时间 (0~1)
        /// 仅当值 >= 0 时才会被应用
        /// </summary>
        public float NormalizedTime;
        /// <summary>
        /// 是否强制执行相位同步
        /// </summary>
        public bool ForcePhaseSync;

        /// <summary>
        /// 默认配置(全身层)
        /// </summary>
        public static AnimPlayOptions Default => new AnimPlayOptions
        {
            Layer = 0,
            FadeDuration = -1f,
            Speed = -1f,
            NormalizedTime = -1f,
            ForcePhaseSync = false
        };

        /// <summary>
        /// 上半身默认配置
        /// </summary>
        public static AnimPlayOptions UpperBodyDefault => new AnimPlayOptions
        {
            Layer = 1,
            FadeDuration = 1f,
            Speed = -1f,
            NormalizedTime = -1f,
            ForcePhaseSync = false
        };
    }
}
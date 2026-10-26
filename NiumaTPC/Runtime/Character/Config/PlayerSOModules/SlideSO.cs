using NiumaTPC.Character.Core.Animation;
using UnityEngine;

namespace NiumaTPC.Character.Config.PlayerSOModules
{
    /// <summary>
    /// 滑铲规则与表现配置。
    /// 权威位移参数会被转换为固定 Tick 配置；
    /// 动画字段只供表现状态机使用。
    /// </summary>
    [CreateAssetMenu(fileName = "SlideSO", menuName = "NiumaTPC/Player/Modules/SlideSO")]
    public class SlideSO : ScriptableObject
    {
        #region Feature Switch(功能开关)

        [Header("功能开关")]

        [Tooltip("是否启用滑铲。关闭后 Ctrl 仍可作为 Walk 使用，但不会启动滑铲。")]
        public bool EnableSliding = true;

        #endregion

        #region Authoritative Motion(权威运动)

        [Header("权威运动")]

        [Tooltip("允许开始滑铲的最低水平速度，单位 m/s。")]
        [Min(0f)]
        public float MinimumStartSpeed = 3.5f;

        [Tooltip("滑铲初速度相对于进入前实际速度的倍率。")]
        [Min(0f)]
        public float StartSpeedMultiplier = 1.15f;

        [Tooltip("滑铲初速度上限，防止异常属性产生过高速度。")]
        [Min(0f)]
        public float MaximumStartSpeed = 8f;

        [Tooltip("滑铲每秒减少的速度，单位 m/s²。")]
        [Min(0.01f)]
        public float Deceleration = 3f;

        [Tooltip("速度衰减到该值或以下时结束滑铲，单位 m/s。")]
        [Min(0f)]
        public float ExitSpeed = 1f;

        [Tooltip("允许跳跃打断前必须经过的时间。用于保证滑铲动作至少可见一小段。")]
        [Range(0.1f, 0.15f)]
        public float MinimumDurationSeconds = 0.12f;

        [Tooltip("滑铲最长持续时间。达到后即使速度仍然较高也会强制结束。")]
        [Min(0.15f)]
        public float MaximumDurationSeconds = 0.8f;

        [Tooltip("滑铲期间是否继续应用重力。地面滑铲通常应该开启。")]
        public bool ApplyGravity = true;

        #endregion

        #region Presentation(动画表现)

        [Header("动画表现")]

        [Tooltip("进入滑铲时播放的动画。只负责从奔跑姿势过渡到滑铲姿势，不决定权威位移。")]
        public MotionClipData SlideEnterAnimation;

        [Tooltip("持续滑铲期间播放的循环动画。滑铲持续时间由固定 Tick 模拟决定。")]
        public MotionClipData SlideLoopAnimation;

        [Tooltip("正常结束滑铲时播放的退出动画。被跳跃等动作打断时可直接切换到对应动作。")]
        public MotionClipData SlideExitAnimation;

        [Tooltip("进入滑铲动画时使用的淡入参数。")]
        public AnimPlayOptions SlideEnterOptions = AnimPlayOptions.Default;

        [Tooltip("滑铲结束后进入待机状态的淡入参数。")]
        public AnimPlayOptions SlideToIdleOptions = AnimPlayOptions.Default;

        [Tooltip("滑铲结束后进入移动循环的淡入参数。")]
        public AnimPlayOptions SlideToMoveOptions = AnimPlayOptions.Default;

        
        #endregion
    }
}

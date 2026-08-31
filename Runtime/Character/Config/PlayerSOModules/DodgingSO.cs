using NiumaTPC.Character.Core.Animation;
using UnityEngine;

namespace NiumaTPC.Character.Config.PlayerSOModules
{
    /// <summary>
    /// 闪避系统配置模块。
    /// 八方向 WarpedMotionData 负责选择动画，实际位移由 MotionDriver 按下方配置独立驱动。
    /// </summary>
    [CreateAssetMenu(fileName = "DodgingSO", menuName = "NiumaTPC/Player/Modules/DodgingSO")]
    public class DodgingSO : ScriptableObject
    {
        [Header("淡入参数 (Fade In Options) - 闪避结束时的动画还原")]
        
        [Tooltip("从闪避回到待机时的淡入参数")]
        public AnimPlayOptions FadeInIdleOptions;
        
        [Tooltip("从闪避回到移动循环时的淡入参数")]
        public AnimPlayOptions FadeInMoveLoopOptions;

        [Header("闪避位移")]

        [Tooltip("闪避产生的总位移，单位为米。位移由 MotionDriver 执行，不依赖动画根运动。")]
        [Min(0f)]
        public float DistanceMeters = 2.65f;

        [Tooltip("完成闪避位移需要的时间，单位为秒。")]
        [Min(0.01f)]
        public float DurationSeconds = 0.4f;

        [Tooltip("闪避的累计位移进度曲线。横轴为时间比例，纵轴为已完成的距离比例，应从 (0,0) 平滑增长到 (1,1)。")]
        public AnimationCurve ProgressCurve =
            AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Tooltip("闪避期间是否继续应用重力。地面闪避建议开启。")]
        public bool ApplyGravity = true;

        [Header("闪避动画")]
        
        public WarpedMotionData ForwardDodge;
        public WarpedMotionData BackwardDodge;
        public WarpedMotionData LeftDodge;
        public WarpedMotionData RightDodge;
        public WarpedMotionData ForwardLeftDodge;
        public WarpedMotionData ForwardRightDodge;
        public WarpedMotionData BackwardLeftDodge;
        public WarpedMotionData BackwardRightDodge;
    }
}

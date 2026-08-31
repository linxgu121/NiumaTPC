using NiumaTPC.Character.Core.Animation;
using UnityEngine;

namespace NiumaTPC.Character.Config.PlayerSOModules
{
    /// <summary>
    /// 翻滚系统配置模块
    /// </summary>
     
    [CreateAssetMenu(fileName = "RollSO", menuName = "NiumaTPC/Player/Modules/RollSO")]
    public class RollSO : ScriptableObject
    {
        [Header("淡入参数")]
        
        [Tooltip("从翻滚回到待机时的淡入参数")]
        public AnimPlayOptions FadeInIdleOptions;
        
        [Tooltip("从翻滚回到移动循环时的淡入参数")]
        public AnimPlayOptions FadeInMoveLoopOptions;

        [Header("翻滚位移")]

        [Tooltip("翻滚产生的总位移，单位为米。位移由 MotionDriver 执行，不依赖动画根运动。")]
        [Min(0f)]
        public float DistanceMeters = 2.75f;

        [Tooltip("完成翻滚位移需要的时间，单位为秒。")]
        [Min(0.01f)]
        public float DurationSeconds = 0.6f;

        [Tooltip("翻滚的累计位移进度曲线。横轴为时间比例，纵轴为已完成的距离比例，应从 (0,0) 平滑增长到 (1,1)。")]
        public AnimationCurve ProgressCurve =
            AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Tooltip("翻滚期间是否继续应用重力。地面翻滚建议开启。")]
        public bool ApplyGravity = true;

        [Header("翻滚")]
        public WarpedMotionData ForwardRoll;
        public WarpedMotionData BackwardRoll;
        public WarpedMotionData LeftRoll;
        public WarpedMotionData RightRoll;
        public WarpedMotionData ForwardLeftRoll;
        public WarpedMotionData ForwardRightRoll;
        public WarpedMotionData BackwardLeftRoll;
        public WarpedMotionData BackwardRightRoll;
    }
}

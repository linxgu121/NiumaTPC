using System.Collections;
using System.Collections.Generic;
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

        [Header("权威位移")]
        [Tooltip("翻滚产生的总位移，单位为米。")]
        [Min(0f)]
        public float DistanceMeters = 2.75f;

        [Tooltip("翻滚权威位移的持续时间，单位为秒。")]
        [Min(0.01f)]
        public float DurationSeconds = 0.6f;

         [Tooltip("翻滚累计位移进度曲线横轴为时间比例，纵轴为已完成的距离比例，应从(0,0)平滑增长至(1,1).如果不懂就不要动这个配置")]
        public AnimationCurve ProgressCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Tooltip("翻滚期间是否继续应用重力")]
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
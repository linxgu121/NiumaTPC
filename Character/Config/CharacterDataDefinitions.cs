using System;
using System.Collections;
using System.Collections.Generic;
using Animancer;
using NiumaTPC.Item.Motion.MotionEnums;
using UnityEngine;

namespace NiumaTPC.Item.Config
{
    /// <summary>
    /// 适用于标准、曲线驱动的运动动画片段数据
    /// 如起步、停止等
    /// </summary>
    [Serializable]
    public class MotionClipData
    {
        [Header("动画资源")]
        [Tooltip("动画片段")]
        public ClipTransition Clip;
        [Tooltip("驱动方式")]
        public MotionType Type = MotionType.CurveDriven;

        [Header("播放设置")]
        public float TargetDuration = 0f;
        public float EndTime = 0f;

        [Header("已被废弃 局部方向矫正(弃用,改为WarpedMotionData的局部速度曲线)")]
        public bool AllowBakeTargetLocalDirection;
        public Vector3 TargetLocalDirection;

        [Header("烘焙运行时数据")]
        public FootPhase EndPhase = FootPhase.LeftFootDown;
        public float PlaybackSpeed = 1f;
        public AnimationCurve SpeedCurve;
        public AnimationCurve RotationCurve;
        public float RotationFinishedTime = 0f;

        public MotionClipData()
        {
            SpeedCurve = new AnimationCurve();
            RotationCurve = new AnimationCurve();
        }

    }

    /// <summary>
    /// 定义动画中用于空间对齐的特征时刻
    /// </summary>
    [Serializable]
    public class WarpPointDef
    {
         [Tooltip("特征点识别名称")]
        public string PointName;

        [Tooltip("触发该特征点的动画归一化时间 (0-1)")]
        [Range(0f, 1f)]
        public float NormalizedTime;

        [Tooltip("在此时刻，对运行时目标点施加的局部坐标偏移")]
        public Vector3 TargetPositionOffset;

        [Header("Baking Results - 烘焙结果")]
        [Tooltip("从上个点到此时刻的烘焙局部位移")]
        public Vector3 BakedLocalOffset;

        [Tooltip("此时刻的烘焙局部旋转")]
        public Quaternion BakedLocalRotation = Quaternion.identity;
    }

    /// <summary>
    /// 用于高级空间扭曲
    /// 如翻越、闪避的动画数据
    /// </summary>
    [Serializable]
    public class WarpedMotionData
    {
        [Header("动画资源")]
        public ClipTransition Clip;

         [Header("时序控制")]
        public float EndTime = 0f;
        public FootPhase EndPhase = FootPhase.LeftFootDown;

        [Header("烘焙与扭曲配置")]
        [Tooltip("定义烘焙器如何自动探测特征点")]
        public WarpedType Type = WarpedType.None;

        [Tooltip(" 空间对齐特征点序列，需按时间升序排列")]
        public List<WarpPointDef> WarpPoints = new List<WarpPointDef>();

        [Tooltip("动作期间手部 IK 的权重曲线")]
        public AnimationCurve HandIKWeightCurve = new AnimationCurve();

        [Header("烘焙曲线（只读）")]
        public float BakedDuration;
        public AnimationCurve LocalVelocityX = new AnimationCurve();
        public AnimationCurve LocalVelocityY = new AnimationCurve();
        public AnimationCurve LocalVelocityZ = new AnimationCurve();
        public AnimationCurve LocalRotationY = new AnimationCurve();

        /// <summary>
        /// 总烘焙本地偏移量
        /// </summary>
        [HideInInspector]
        public Vector3 TotalBakedLocalOffset;

        [Header("Physics")]
        [Tooltip("如果启用，在这段动画运动期间会应用重力。否则，忽略重力带来的垂直运动。")]
        public bool ApplyGravity = false; // 默认 false 保持向后兼容，只有在需要时启用重力
    
    }

}

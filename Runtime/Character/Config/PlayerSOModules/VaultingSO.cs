using System.Collections;
using System.Collections.Generic;
using NiumaTPC.Character.Core.Animation;
using UnityEngine;

namespace NiumaTPC.Character.Config.PlayerSOModules
{
    /// <summary>
    /// 翻越系统配置模块
    /// 它负责管理越过障碍物的所有参数 包括检测 动画 IK等
    /// 翻越是复杂的多层协调动作 改这里的参数时要同时改对应的动画数据 不然会断手断脚 
    /// </summary>
    [CreateAssetMenu(fileName = "VaultionSO", menuName = "NiumaTPC/Player/Modules/VaultingSO")]
    public class VaultingSO : ScriptableObject
    {
        [Header("翻越检测")]
        
        [Tooltip("障碍物层级")]
        public LayerMask ObstacleLayers = ~0;

        [Tooltip("可站立地面层级。用于检测墙顶和翻越后的落点；地面不在障碍层时必须配置，否则翻越会找不到落点。")]
        public LayerMask GroundLayers = ~0;

        [Tooltip("前向射线长度")]
        public float VaultForwardRayLength = 1.5f;

        [Tooltip("前向射线高度")]
        public float VaultForwardRayHeight = 1.0f;

        [Tooltip("下向射线偏移 从前向射线命中点向下这个距离发射下向射线 寻找墙顶部的落脚点")]
        public float VaultDownwardRayOffset = 0.5f;

        [Tooltip("下向射线长度")]
        public float VaultDownwardRayLength = 2.0f;

        [Space]
        [Tooltip("双手抓握点宽度")]
        public float VaultHandSpread = 0.4f;

        [Tooltip("落脚点搜索距离")]
        public float VaultLandDistance = 1.5f;

        [Tooltip("落脚点额外前推距离。角色总是卡在墙顶或方块边缘时调大；如果落点太靠后会滑下去，建议从 0.25 到 0.5 之间调。")]
        public float VaultLandForwardPadding = 0.35f;

        [Tooltip("落脚点射线长度")]
        public float VaultLandRayLength = 3.0f;

        [Tooltip("是否需要墙后有地面")]
        public bool RequireGroundBehindWall = true;

        [Header("翻越高度分级")]
        
        [Tooltip("低翻越最小高度")]
        public float LowVaultMinHeight = 0.5f;
        
        [Tooltip("低翻越最大高度")]
        public float LowVaultMaxHeight = 1.2f;

        [Space]
        [Tooltip("高翻越最小高度")]
        public float HighVaultMinHeight = 1.2f;
        
        [Tooltip("高翻越最大高度")]
        public float HighVaultMaxHeight = 2.5f;

        [Header("低翻越权威轨迹")]
        [Tooltip("低翻越持续时间,单位秒,应与低翻越动画的主要动作时长接近")]
        [Min(0.05f)]
        public float LowVaultDurationSeconds = 0.8f;

        [Tooltip("低翻越第一阶段结束时刻,使用整个动作的归一化时间,0.45 表示前45%时间到达墙沿")]
        [Range(0.05f, 0.95f)]
        public float LowVaultFirstStageEndNormalizedTime = 0.45f;

        [Tooltip("低翻越从起点到墙沿的累计进度曲线，必须从(0,0)单调增长到(1,1)。")]
        public AnimationCurve LowVaultFirstStageProgressCurve =
            AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Tooltip("低翻越从墙沿到最终落点的累计进度曲线，必须从(0,0)单调增长到(1,1)。")]
        public AnimationCurve LowVaultSecondStageProgressCurve =
            AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Tooltip("低翻越朝向目标角度的累计旋转进度。")]
        public AnimationCurve LowVaultRotationProgressCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("高翻越权威轨迹")]
        [Tooltip("高翻越持续时间，单位秒。")]
        [Min(0.05f)]
        public float HighVaultDurationSeconds = 1.1f;

        [Tooltip("高翻越第一阶段结束时刻。高翻越通常需要更长时间到达墙沿。")]
        [Range(0.05f, 0.95f)]
        public float HighVaultFirstStageEndNormalizedTime = 0.55f;

        [Tooltip("高翻越从起点到墙沿的累计进度曲线。")]
        public AnimationCurve HighVaultFirstStageProgressCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Tooltip("高翻越从墙沿到最终落点的累计进度曲线。")]
        public AnimationCurve HighVaultSecondStageProgressCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Tooltip("高翻越朝向目标角度的累计旋转进度。")]
        public AnimationCurve HighVaultRotationProgressCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);


        [Header("翻越动画数据")]
        [Tooltip("翻越结束后到待机状态的淡入参数")]
        public AnimPlayOptions VaultToIdleOptions = AnimPlayOptions.Default;
        
        [Tooltip("翻越结束后到移动循环的淡入参数")]
        public AnimPlayOptions VaultToMoveOptions = AnimPlayOptions.Default;

        [Tooltip("低翻越")]
        public WarpedMotionData lowVaultAnim;

        [Tooltip("高翻越")]
        public WarpedMotionData highVaultAnim;

        [Header("IK 偏移 以 ledge 朝向为基准的本地偏移")]
        [Tooltip("左手 IK 目标在 ledge 局部空间下的偏移（以 ledge 朝向/竖直为基准）")]
        public Vector3 LeftHandIKOffset = Vector3.zero;

        [Tooltip("右手ik偏移")]
        public Vector3 RightHandIKOffset = Vector3.zero;

        [Tooltip("手部朝向的欧拉角偏移")]
        public Vector3 HandRotationOffsetEuler = Vector3.zero;
    }
}

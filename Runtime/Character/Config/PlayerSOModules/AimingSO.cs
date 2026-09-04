using Animancer;
using UnityEngine;

namespace NiumaTPC.Character.Config.PlayerSOModules
{
    [CreateAssetMenu(fileName = "AimingSO", menuName = "NiumaTPC/Player/Modules/AimingSO")]
    public class AimingSO : ScriptableObject
    {
        [Header("灵敏度")]
        
        [Tooltip("瞄准时的鼠标灵敏度倍数")]
        public float AimSensitivity = 1f;
        
        [Header("瞄准移动速度")]
        
        [Tooltip("瞄准时行走速度 m/s")]
        public float AimWalkSpeed = 1.5f;
        
        [Tooltip("瞄准时慢跑速度 m/s")]
        public float AimJogSpeed = 2.5f;
        
        [Tooltip("瞄准时冲刺速度 m/s")]
        public float AimSprintSpeed = 5.0f;
        
        [Header("旋转与混合参数")]
        
        [Tooltip("瞄准时的旋转平滑时间")]
        public float AimRotationSmoothTime = 0.05f;

        [Tooltip("瞄准前后(X)动画参数平滑时间")]
        public float AimXAnimBlendSmoothTime = 0.2f;
        
        [Tooltip("瞄准左右(Y)动画参数平滑时间")]
        public float AimYAnimBlendSmoothTime = 0.2f;
        
        [Tooltip("瞄准目标IK追踪平滑时间(重要！这个决定了角色拉枪到准星的速度)")]
        public float AimIkChaseSmoothTime = 0.1f;

        [Header("远端瞄准表现")]
        [Min(1f)]
        [Tooltip("远端观察者根据服务器 Yaw 与 ViewPitch 重建瞄准 IK 目标时使用的距离，仅影响动画表现，不参与射线、命中或伤害计算。推荐 50 米。")]
        public float AimPresentationDistance = 50f; 

        [Header("动画资源")]

        [Tooltip("瞄准 Walk 状态的2D混合树 参数 (x,y) 统一映射到半径为1的圆内坐标")]
        public MixerTransition2D AimWalkMixer;

        [Tooltip("瞄准 Jog 状态的2D混合树 参数 (x,y) 统一映射到半径为1的圆内坐标")]
        public MixerTransition2D AimJogMixer;

        [Tooltip("瞄准 Sprint 状态的2D混合树 参数 (x,y) 统一映射到半径为1的圆内坐标")]
        public MixerTransition2D AimSprintMixer;

    }
}
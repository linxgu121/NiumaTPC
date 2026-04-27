using System.Collections;
using System.Collections.Generic;
using NiumaTPC.Character.Config.PlayerSOModules;
using NiumaTPC.Character.Motion.MotionEnums;
using NiumaTPC.Character.State;
using UnityEngine;

namespace NiumaTPC.Character.Config
{
    [CreateAssetMenu(fileName = "PlayerConfig_Main", menuName = "NiumaTPC/Player/PlayerConfig(Main)")]
    public class PlayerSO : ScriptableObject
    {
        [Header("核心功能模块")]

        [Tooltip("角色状态机与全局打断逻辑注册表")]
        public PlayerBrainSO Brain;

        [Tooltip("基础物理参数模块")]
        public CoreSO Core;

        [Tooltip("基础移动动画集合")]
        public LocomotionSO LocomotionAnims;
        
        [Tooltip("跳跃与落地系统")]
        public JumpSO JumpAndLanding;

        [Tooltip("瞄准系统参数")]
        public AimingSO Aiming;

        [Header("高级模块 ")]
        public VaultingSO Vaulting;
        public DodgingSO Dodging;
        public RollSO Rolling;
        public ActionSO Action;
        public AudioSO Audio;
        public EmjSO Emj;
    }
}
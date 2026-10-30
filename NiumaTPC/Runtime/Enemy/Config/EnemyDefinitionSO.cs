using NiumaTPC.Enemy.Combat;
using NiumaTPC.Enemy.Navigation;
using NiumaTPC.Role;
using UnityEngine;
using UnityEngine.AI;

namespace NiumaTPC.Enemy.Config
{
    /// <summary>
    /// 定义一种敌人的身份、角色模板和AI基础参数
    /// 不保存单个敌人实例的运行时状态
    /// </summary>
    [CreateAssetMenu(fileName = "EnemyDefinition", menuName = "NiumaTPC/Enemy/EnemyDefinition")]
    public sealed class EnemyDefinitionSO : ScriptableObject
    {
        #region Identity(敌人身份)

        [Header("敌人身份")]

        [Tooltip("敌人类型的稳定编号，不是角色模板编号")]
        public ushort EnemyId;

        [Tooltip("敌人显示名称的本地化键")]
        public string DisplayNameKey;

        #endregion

        #region Character(角色模板)

        [Header("角色模板")]

        [Tooltip("提供该敌人的基础生命、移动速度和角色模板数据")]
        public CharacterDefinitionSO CharacterDefinition;

        #endregion

        #region Combat(战斗配置)

        [Header("怪物攻击")]

        [Tooltip("普通怪物自身拥有的主要攻击配置，不代表可拾取或切换的装备")]
        public EnemyAttackProfileSO AttackProfile;

        #endregion

        #region Perception(感知配置)

        [Header("感知配置")]

        [Min(0.1f)]
        [Tooltip("敌人首次发现目标的感知半径，单位米")]
        public float DetectionRadius = 12f;

        [Range(0f, 180f)]
        [Tooltip("目标相对敌人正前方允许的最大夹角，90表示覆盖整个前方半圆")]
        public float MaximumTargetAngleFromForward = 75f;

        [Min(0.1f)]
        [Tooltip("已有目标超过该距离后立即丢失，建议大于首次感知半径")]
        public float LoseTargetDistance = 18f;

        [Min(0f)]
        [Tooltip("目标离开视野后继续记住最后位置的时间，单位秒")]
        public float TargetMemorySeconds = 2f;

        #endregion

        #region Navigation(导航配置)

        [Header("导航配置")]

        [Tooltip("追击目标时使用的路径规划方式，Direct适合空旷测试场景，NavMesh适合正式关卡")]
        public EnemyNavigationMode NavigationMode = EnemyNavigationMode.Direct;

        [Tooltip("允许敌人经过的NavMesh Area位掩码，-1表示允许所有Area")]
        public int NavMeshAreaMask = NavMesh.AllAreas;

        [Min(0.01f)]
        [Tooltip("角色或目标距离NavMesh多远时仍允许投影到导航表面，单位米")]
        public float NavMeshSampleDistance = 2f;

        [Min(0.01f)]
        [Tooltip("角色距离当前路径拐点多近时切换到下一个拐点，单位米")]
        public float NavMeshCornerReachDistance = 0.2f;

        #endregion

        #region Decision(决策配置)

        [Header("决策配置")]

        [Min(0.02f)]
        [Tooltip("两次AI目标与状态决策之间的最短间隔，单位秒")]
        public float DecisionIntervalSeconds = 0.2f;

        [Min(0.05f)]
        [Tooltip("追击期间重新计算导航路径的最短间隔，单位秒")]
        public float RepathIntervalSeconds = 0.25f;

        [Tooltip("追击距离较远的目标时是否允许使用冲刺输入")]
        public bool AllowSprintChase = true;

        #endregion
    }
}
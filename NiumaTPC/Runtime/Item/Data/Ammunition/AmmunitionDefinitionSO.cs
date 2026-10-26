using UnityEngine;

namespace NiumaTPC.Item
{
    /// <summary>
    /// 一类子弹的静态配置
    /// 不保存具体子弹的位置、速度、剩余寿命等运行时状态
    /// </summary>
    [CreateAssetMenu(fileName = "AmmunitionSO",menuName = "NiumaTPC/Item/Ammunition")]
    public class AmmunitionDefinitionSO : ScriptableObject
    {
        [Tooltip("稳定且唯一的配置编号(像7.62、5.56等)")]
        public string DefinitionId = string.Empty;

        [Header("弹道")]

        [Min(0f)]
        [Tooltip("重力倍率 0 表示不受重力影响 1 表示使用一倍场景重力")]
        public float GravityScale = 0f;

        [Min(0f)]
        [Tooltip("碰撞检测半径 0 使用射线；大于 0 使用球形扫掠")]
        public float CollisionRadius = 0f;

        [Min(0.01f)]
        [Tooltip("子弹最大存活时间")]
        public float MaxLifetime = 5f;

        [Header("纯表现资源")]

        [Tooltip("用于挂载 ProjectileView 的视觉预制体")]
        public GameObject VisualPrefab;

        [Tooltip("命中特效预制体。为空时不播放命中特效。")]
        public GameObject ImpactPrefab;

        [Tooltip("命中音效。为空时不播放。开枪音效仍由武器负责。")]
        public AudioClip ImpactSound;
    }
}

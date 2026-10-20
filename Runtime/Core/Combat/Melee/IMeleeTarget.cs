using UnityEngine;

namespace NiumaTPC.Core.Combat
{
    /// <summary>
    /// 可被近战吸附系统选择的目标
    /// 只描述整名角色，不代表具体受击部位
    /// </summary>
    public interface IMeleeTarget
    {
        /// <summary>
        /// 目标角色根节点
        /// </summary>
        Transform RootTransform { get; }

        /// <summary>
        /// 角色吸附参考位置
        /// 只用于选敌、朝向和接近方向计算
        /// </summary>
        Vector3 AdsorptionPosition { get; }

        /// <summary>
        /// 目标角色水平碰撞半径
        /// 用于计算吸附停止距离
        /// </summary>
        float CollisionRadius { get; }

        /// <summary>
        /// 当前角色是否允许被近战系统选中
        /// </summary>
        bool IsTargetable { get; }
    }
}
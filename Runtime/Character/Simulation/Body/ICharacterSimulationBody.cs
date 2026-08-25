using UnityEngine;

namespace NiumaTPC.Character.Simulation
{
    /// <summary>
    /// 角色模拟器与实际场景碰撞组件之间的边界。
    /// 第一阶段的具体实现会包装 CharacterController。
    /// </summary>
    public interface ICharacterSimulationBody
    {
        /// <summary>
        /// 碰撞处理后的真实世界坐标
        /// </summary>
        Vector3 Position {get; }

        /// <summary>
        /// 当前角色的世界 Yaw
        /// </summary>
        float Yaw {get; }

        /// <summary>
        /// 最近一次移动后是否着地
        /// </summary>
        bool IsGrounded {get; }

        /// <summary>
        /// 提交本 Tick 的世界空间位移。
        /// 具体实现负责碰撞、坡度和台阶处理。
        /// </summary>
        void Move(Vector3 displacement);

        /// <summary>
        /// 应用模拟器计算出的角色朝向
        /// </summary>
        void SetYaw(float yaw);

        /// <summary>
        /// 强制设置角色位置和朝向。
        /// 用于出生、传送以及服务器 Reconcile 校正。
        /// </summary>
        void SetPose(Vector3 position, float yaw);
    }
}
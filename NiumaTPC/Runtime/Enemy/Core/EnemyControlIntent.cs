using UnityEngine;

namespace NiumaTPC.Enemy.Core
{
    /// <summary>
    /// 保存服务器AI准备交给TPC输入管线的控制意图
    /// 不直接修改角色位置或播放动画
    /// </summary>
    public struct EnemyControlIntent
    {
        /// <summary>
        /// 归一化后的移动输入
        /// </summary>
        public Vector2 Move;

        /// <summary>
        /// AI期望角色面向的世界Yaw
        /// </summary>
        public float ViewYaw;

        /// <summary>
        /// AI期望角色使用的俯仰角
        /// </summary>
        public float ViewPitch;

        /// <summary>
        /// 是否保持冲刺追击
        /// </summary>
        public bool SprintHeld;

        /// <summary>
        /// 是否在当前帧请求一次轻击
        /// </summary>
        public bool LightAttackPressed;

        /// <summary>
        /// 是否在当前帧请求一次重击
        /// </summary>
        public bool HeavyAttackPressed;

        /// <summary>
        /// 清除只允许提交一次的攻击输入
        /// 保留移动、朝向和持续按住状态
        /// </summary>
        public void ClearTransientActions()
        {
            LightAttackPressed = false;
            HeavyAttackPressed = false;
        }

        /// <summary>
        /// 清除全部AI控制意图
        /// 用于死亡、回收和目标失效
        /// </summary>
        public void Clear()
        {
            this = default;
        }
    }
}
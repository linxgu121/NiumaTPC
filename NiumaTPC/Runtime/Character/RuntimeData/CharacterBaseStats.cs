using System;

namespace NiumaTPC.Character.RuntimeData
{
    /// <summary>
    /// 单独角色的属性
    /// (后续角色有多的独享属性再添加)
    /// </summary>
    public class CharacterBaseStats
    {
        /// <summary>
        /// 角色最大基础生命上限
        /// </summary>
        public float MaxHealth { get; }

        /// <summary>
        /// 基础行走速度，m/s
        /// </summary>
        public float WalkSpeed { get; }

        /// <summary>
        /// 基础慢跑速度，m/s
        /// </summary>
        public float JogSpeed { get; }

        /// <summary>
        /// 基础冲刺速度，m/s
        /// </summary>
        public float SprintSpeed { get; }

        public CharacterBaseStats(float maxHealth, float walkSpeed, float jogSpeed, float sprintSpeed)
        {
            // maxHealth：有限正数
            if (!float.IsFinite(maxHealth) || maxHealth <= 0f)
                throw new ArgumentOutOfRangeException(nameof(maxHealth), "MaxHealth必须是有限正数");

            // 三种速度：有限、>=0，允许0
            if (!float.IsFinite(walkSpeed) || walkSpeed < 0f)
                throw new ArgumentOutOfRangeException(nameof(walkSpeed), "WalkSpeed必须是有限非负数");

            if (!float.IsFinite(jogSpeed) || jogSpeed < 0f)
                throw new ArgumentOutOfRangeException(nameof(jogSpeed), "JogSpeed必须是有限非负数");

            if (!float.IsFinite(sprintSpeed) || sprintSpeed < 0f)
                throw new ArgumentOutOfRangeException(nameof(sprintSpeed), "SprintSpeed必须是有限非负数");

            MaxHealth = maxHealth;
            WalkSpeed = walkSpeed;
            JogSpeed = jogSpeed;
            SprintSpeed = sprintSpeed;
        }


    }
}

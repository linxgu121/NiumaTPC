using System;
using UnityEngine;

namespace NiumaTPC.Character.Simulation
{
    /// <summary>
    /// 某个模拟 Tick 中持续生效的按钮状态
    /// 使用位标志把多个 bool 压缩到一个 ushort 中
    /// </summary>
    [Flags]
    public enum CharacterInputButtons : ushort
    {
        None = 0,

        Walk = 1 << 0,

        Sprint = 1 << 1
    }

    /// <summary>
    /// 描述玩家在某个模拟 Tick 中想做什么
    /// 这里只保存输入事实，不直接移动角色
    /// </summary>
    public struct CharacterInputCommand
    {
        /// <summary>
        /// 这条命令属于哪个网络模拟 Tick
        /// </summary>
        public uint Tick;

        /// <summary>
        /// 移动输入，X表示左右，Y表示前后
        /// 合法长度应在0到1之间
        /// </summary>
        public Vector2 Move;

        /// <summary>
        /// 玩家期望面对的世界Y轴角度
        /// </summary>
        public float ViewYaw;

        /// <summary>
        /// 当前Tick中持续生效的按钮集合
        /// </summary>
        public CharacterInputButtons Buttons;

        public CharacterInputCommand(uint tick, Vector2 move, float viewYaw, CharacterInputButtons buttons)
        {
            Tick = tick;
            Move = move;
            ViewYaw = viewYaw;
            Buttons = buttons;
        }

        public bool HasButton(CharacterInputButtons button)
        {
            return (Buttons & button) != 0;
        }
            
    

    }

}


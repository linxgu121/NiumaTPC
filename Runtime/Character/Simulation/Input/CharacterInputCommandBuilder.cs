using NiumaTPC.Character.RuntimeData;
using UnityEngine;

namespace NiumaTPC.Character.Simulation
{
    /// <summary>
    /// 把TPC处理后的输入快照转换为网络模拟命令
    /// 它只负责整理数据，不负责发送数据或移动角色
    /// </summary>
    public sealed class CharacterInputCommandBuilder
    {
        /// <summary>
        /// 为指定网络Tick构建一条角色输入命令
        /// </summary>
        /// <param name="tick">命令所属的网络Tick</param>
        /// <param name="input">TPC当前处理后的输入快照</param>
        /// <param name="viewYaw">地玩家当前期望的世界朝向</param>
        public CharacterInputCommand Build(uint tick, in ProcessedInputData input, float viewYaw)
        {
            //防止斜向输入长度超过1，导致角色斜着移动更快
            Vector2 move = Vector2.ClampMagnitude(input.Move, 1f);

            CharacterInputButtons buttons = CharacterInputButtons.None;

            //Sprint优先于Walk，避免同时按下时产生矛盾命令
            if (input.SprintHeld)
            {
                buttons |= CharacterInputButtons.Sprint;
            }
            else if (input.WalkHeld)
            {
                buttons |= CharacterInputButtons.Walk;
            }
            
            // JumpPressed 带有输入缓冲，可以避免按键发生在两个网络 Tick 之间时丢失。
            if(input.JumpPressed)
            {
                buttons |= CharacterInputButtons.Jump;
            }

            // DodgePressed 和 RollPressed 都来自 InputPipeline 的输入缓冲，
            // 避免按键发生在两个网络 Tick 之间时丢失。
            if(input.DodgePressed)
            {
                buttons |= CharacterInputButtons.Dodge;
            }
            if(input.RollPressed)
            {
                buttons |= CharacterInputButtons.Roll;
            }
            if (input.SlidePressed)
            {
                buttons |= CharacterInputButtons.Slide;
            }

            float normalizedYaw = Mathf.Repeat(viewYaw, 360f);

            return new CharacterInputCommand(tick, move, normalizedYaw, buttons);

            
        }
    }
}
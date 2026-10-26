using NiumaCore.Module;

namespace NiumaCore.ConditionEffect
{
    /// <summary>
    /// 定义游戏效果接口，用于表示和应用游戏中的各种效果，例如状态效果、环境效果、技能效果等
    /// </summary>
    public interface IGameEffect
    {
        /// <summary>
        /// 应用效果，接受一个游戏上下文参数，允许模块根据当前的游戏状态和上下文信息应用效果，以便在游戏逻辑中进行相应的处理和决策\
        /// 例如在角色系统中应用状态效果，在环境系统中应用环境效果，在技能系统中应用技能效果等
        /// </summary>
        void Apply(GameContext context);
    }
}

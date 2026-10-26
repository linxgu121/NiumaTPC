using NiumaCore.Module;

namespace NiumaCore.ConditionEffect
{
    /// <summary>
    /// 定义游戏条件接口，用于表示和评估游戏中的各种条件，例如任务完成条件、角色状态条件、环境条件等
    /// 游戏条件通常包含一个IsMet方法，接受一个游戏上下文参数，允许模块根据当前的游戏状态和上下文信息评估条件是否满足，以便在游戏逻辑中进行相应的处理和决策，
    /// 例如在任务系统中评估任务完成条件，在角色系统中评估角色状态条件，在环境系统中评估环境条件等
    /// </summary>
    public interface IGameCondition
    {
        /// <summary>
        /// 评估条件是否满足，接受一个游戏上下文参数，允许模块根据当前的游戏状态和上下文信息评估条件是否满足，以便在游戏逻辑中进行相应的处理和决策
        /// </summary>
        bool IsMet(GameContext context);
    }
}

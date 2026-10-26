namespace NiumaCore.Input
{
    /// <summary>
    /// 定义游戏输入阻止器接口，用于控制游戏输入的启用和禁用
    /// </summary>
    public interface IGameplayInputBlocker
    {
        /// <summary>
        /// 输入是否被阻止
        /// </summary>
        bool IsBlocked { get; }

        /// <summary>
        /// 阻止输入，接受一个原因参数，允许模块根据特定的原因来控制输入的启用和禁用，例如在游戏菜单打开时阻止游戏输入，在对话框显示时阻止玩家移动等
        /// </summary>
        void Block(string reason);
        /// <summary>
        /// 解除输入阻止，接受一个原因参数，允许模块根据特定的原因来控制输入的启用和禁用，例如在游戏菜单关闭时解除输入阻止，在对话框关闭时恢复玩家移动等
        /// </summary>
        void Unblock(string reason);
    }
}

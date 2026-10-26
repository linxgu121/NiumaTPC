using UnityEngine;

namespace NiumaCore.Player
{
    /// <summary>
    /// 定义玩家模块接口，用于管理玩家相关的功能和状态
    /// 玩家模块负责处理玩家的输入、控制玩家的移动和行为、管理玩家的状态和属性等功能，以实现玩家在游戏中的交互和体验
    /// 玩家模块可以通过事件总线与其他模块进行通信，例如在玩家死亡时发布一个玩家死亡事件，其他模块可以订阅该事件并执行相应的逻辑，例如在UI模块中显示玩家死亡界面，在音频模块中播放玩家死亡音效等
    /// </summary>
    public interface IPlayerModule
    {
        /// <summary>
        /// 玩家控制是否启用，允许模块根据特定的条件来控制玩家的输入和行为，例如在游戏菜单打开时禁用玩家控制，在对话框显示时禁用玩家移动等
        /// </summary>
        bool IsControlEnabled { get; }

        /// <summary>
        /// 玩家的Transform组件，允许模块访问和控制玩家的位置、旋转和缩放等属性，例如在玩家移动时更新玩家的Transform，在玩家被击中时播放受击动画等
        /// </summary>
        Transform PlayerTransform { get; }

        /// <summary>
        /// 启用玩家控制，允许模块在需要玩家输入和行为时启用玩家控制，例如在游戏开始时启用玩家控制，在对话框关闭时启用玩家控制等
        /// </summary>
        void EnableControl();
        /// <summary>
        /// 禁用玩家控制，允许模块在不需要玩家输入和行为时禁用玩家控制，例如在游戏菜单打开时禁用玩家控制，在对话框显示时禁用玩家控制等
        /// </summary>
        void DisableControl(string reason);
        /// <summary>
        /// 传送玩家，接受一个位置参数和一个旋转参数，允许模块根据特定的条件来传送玩家，例如在玩家进入传送区域时传送玩家到指定位置，在玩家完成任务时传送玩家到奖励区域等
        /// </summary>
        void Teleport(Vector3 position, Quaternion rotation);
        /// <summary>
        /// 杀死玩家，接受一个原因参数，允许模块根据特定的条件来杀死玩家，例如在玩家掉入陷阱时杀死玩家，在玩家被敌人攻击时杀死玩家等
        /// </summary>
        void Kill(string reason);
    }
}

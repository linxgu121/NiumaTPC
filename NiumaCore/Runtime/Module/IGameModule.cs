namespace NiumaCore.Module
{
    /// <summary>
    /// 定义游戏模块的接口
    /// 用于组织和管理游戏逻辑的不同部分
    /// 每个模块均可拥有独立的初始化、启动、停止和帧更新逻辑，以此实现游戏架构中关注点的有效分离与模块化设计。
    /// 模块可以通过事件总线与其他模块进行通信，或者访问游戏上下文中的服务和资源。
    /// </summary>
    public interface IGameModule
    {
        /// <summary>
        /// 模块名称，用于标识和管理模块
        /// </summary>
        string ModuleName { get; }

        /// <summary>
        /// 模块初始化方法，接受一个游戏上下文参数，允许模块在启动前进行必要的设置和资源加载
        /// </summary>
        void Initialize(GameContext context);
        /// <summary>
        /// 模块启动方法，通常在游戏开始或模块被激活时调用，用于执行模块的主要功能和逻辑
        /// </summary>
        void StartModule();
        /// <summary>
        /// 模块停止方法，通常在游戏结束或模块被禁用时调用，用于清理资源、取消订阅事件和执行必要的收尾工作
        /// </summary>
        void StopModule();
        /// <summary>
        /// 模块帧更新方法，接受一个deltaTime参数，允许模块在每一帧进行逻辑更新和处理游戏状态的变化
        /// </summary>
        void Tick(float deltaTime);
    }
}
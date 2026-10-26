namespace NiumaCore.UI
{
    /// <summary>
    /// 定义UI视图接口，用于管理游戏中的UI界面
    /// 每个视图均可拥有独立的打开、关闭、刷新和帧更新逻辑，以此实现UI架构中关注点的有效分离与模块化设计
    /// 视图可以通过事件总线与其他模块进行通信，或者访问游戏上下文中的服务和资源
    /// </summary>
    public interface IUIView
    {
        /// <summary>
        /// 视图ID，用于标识和管理视图
        /// </summary>
        string ViewId { get; }

        /// <summary>
        /// 打开视图，接受一个可选的payload参数，允许模块在打开视图时传递必要的数据和上下文信息，例如在打开角色界面时传递角色ID，在打开背包界面时传递物品列表等
        /// </summary>
        void Open(object payload = null);
        /// <summary>
        /// 关闭视图，允许模块在不需要显示视图时将其隐藏或销毁，以释放资源和优化性能，例如在关闭角色界面时隐藏界面元素，在关闭背包界面时销毁物品列表等
        /// </summary>
        void Close();
        /// <summary>
        /// 刷新视图，接受一个可选的payload参数，允许模块在需要更新视图内容时传递必要的数据和上下文信息，例如在角色属性发生变化时刷新角色界面，在背包物品发生变化时刷新背包界面等
        /// </summary>
        void Refresh(object payload = null);
        /// <summary>
        /// 视图帧更新方法，接受一个deltaTime参数，允许模块在每一帧进行逻辑更新和处理视图状态的变化，例如在角色界面中更新动画效果，在背包界面中处理物品拖拽等
        /// </summary>
        void Tick(float deltaTime);
    }
}

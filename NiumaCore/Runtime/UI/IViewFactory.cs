namespace NiumaCore.UI
{
    /// <summary>
    /// 定义视图工厂接口，用于创建和管理UI视图实例
    /// 视图工厂负责根据视图ID创建对应的视图实例，并提供释放视图实例的功能，以便在不需要显示视图时将其隐藏或销毁，释放资源和优化性能
    /// </summary>
    public interface IViewFactory
    {
        /// <summary>
        /// 获取视图实例，接受一个视图ID参数，允许模块根据视图ID获取对应的视图实例，例如在打开角色界面时获取角色界面实例，在打开背包界面时获取背包界面实例等
        /// </summary>
        IUIView Get(string viewId);
        /// <summary>
        /// 释放视图实例，接受一个视图ID参数，允许模块根据视图ID释放对应的视图实例，例如在关闭角色界面时释放角色界面实例，在关闭背包界面时释放背包界面实例等
        /// </summary>
        void Release(string viewId);
    }
}
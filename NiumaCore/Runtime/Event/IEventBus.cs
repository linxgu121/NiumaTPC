using System;

namespace NiumaCore.Event
{
    /// <summary>
    /// 定义事件总线接口，用于模块之间的通信和事件传递
    /// 用于模块间解耦通信，支持发布/订阅/取消订阅强类型事件
    /// </summary>
    public interface IEventBus
    {
        /// <summary>
        /// 发布事件，允许模块将事件发送到事件总线，其他订阅了该事件类型的模块将会收到通知并执行相应的处理逻辑
        /// </summary>
        void Publish<T>(T evt);

        /// <summary>
        /// 按指定信道发布事件。
        /// Immediate 表示立即同步派发；Deferred 表示进入延迟队列，由统一时机批量派发。
        /// </summary>
        void Publish<T>(T evt, EventChannel channel);

        /// <summary>
        /// 订阅事件，允许模块注册一个事件处理器，当指定类型的事件被发布时，处理器将会被调用以执行相应的逻辑
        /// </summary>
        void Subscribe<T>(Action<T> handler);
        /// <summary>
        /// 取消订阅事件，允许模块注销之前注册的事件处理器，以停止接收指定类型的事件通知
        /// </summary>
        void Unsubscribe<T>(Action<T> handler);

        /// <summary>
        /// 派发延迟信道中的事件。
        /// 建议通过 GameContext.DrainDeferredEvents 统一调用，并放在所有模块 Tick 完成之后。
        /// </summary>
        void DrainDeferred(int maxEvents = int.MaxValue);
    }
}

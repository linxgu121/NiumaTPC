using System;
using System.Collections.Generic;

namespace NiumaCore.Event
{
    /// <summary>
    /// SimpleEventBus是一个简单的事件总线实现，提供了发布、订阅和取消订阅事件的功能
    /// 通过使用一个字典来存储事件类型和对应的处理器，允许模块之间进行解耦通信，支持强类型事件的发布和订阅
    /// 当事件被发布时，所有订阅了该事件类型的处理器将会被调用以执行相应的逻辑，从而实现模块间的有效通信和事件传递
    /// 适用于需要简单事件系统的游戏架构，提供了基础的事件管理功能，便于模块之间的交互和通信
    /// 注意：该实现不支持事件优先级、事件过滤或异步事件处理等高级功能，适用于简单的事件传递需求
    /// 如果需要更复杂的事件系统，可以考虑使用第三方库或扩展该实现以满足特定需求
    /// </summary>
    public sealed class SimpleEventBus : IEventBus
    {
        private readonly struct QueuedEvent
        {
            public readonly Action Dispatch;

            public QueuedEvent(Action dispatch)
            {
                Dispatch = dispatch;
            }
        }

        /// <summary>
        /// 事件处理器字典，键为事件类型，值为对应的事件处理器委托
        /// </summary>
        private readonly Dictionary<Type, Delegate> _handlers = new();

        /// <summary>
        /// 延迟事件队列。用于把非关键即时事件推迟到统一时机派发，避免一帧内事件链过深。
        /// </summary>
        private readonly Queue<QueuedEvent> _deferredEvents = new();

        /// <summary>
        /// 发布事件，允许模块将事件发送到事件总线，其他订阅了该事件类型的模块将会收到通知并执行相应的处理逻辑
        /// </summary>
        public void Publish<T>(T evt)
        {
            Publish(evt, EventChannel.Immediate);
        }

        /// <summary>
        /// 按指定信道发布事件。Immediate 立即同步派发，Deferred 进入延迟队列等待 DrainDeferred。
        /// </summary>
        public void Publish<T>(T evt, EventChannel channel)
        {
            if (channel == EventChannel.Deferred)
            {
                _deferredEvents.Enqueue(new QueuedEvent(() => Dispatch(evt)));
                return;
            }

            Dispatch(evt);
        }

        private void Dispatch<T>(T evt)
        {
            if (_handlers.TryGetValue(typeof(T), out var del))
            {
                var callback = del as Action<T>;
                callback?.Invoke(evt);
            }
        }

        /// <summary>
        /// 订阅事件，允许模块注册一个事件处理器，当指定类型的事件被发布时，处理器将会被调用以执行相应的逻辑
        /// </summary>
        public void Subscribe<T>(Action<T> handler)
        {
            var type = typeof(T);

            if (_handlers.TryGetValue(type, out var existing))
                _handlers[type] = Delegate.Combine(existing, handler);
            else
                _handlers[type] = handler;
        }

        /// <summary>
        /// 取消订阅事件，允许模块注销之前注册的事件处理器，以停止接收指定类型的事件通知
        /// </summary>
        public void Unsubscribe<T>(Action<T> handler)
        {
            var type = typeof(T);

            if (!_handlers.TryGetValue(type, out var existing))
                return;

            var current = Delegate.Remove(existing, handler);

            if (current == null)
                _handlers.Remove(type);
            else
                _handlers[type] = current;
        }

        /// <summary>
        /// 派发延迟队列中的事件。
        /// maxEvents 用于限制单帧最多处理数量，避免延迟事件过多时卡住主线程。
        /// </summary>
        public void DrainDeferred(int maxEvents = int.MaxValue)
        {
            var processed = 0;
            while (_deferredEvents.Count > 0 && processed < maxEvents)
            {
                var queuedEvent = _deferredEvents.Dequeue();
                queuedEvent.Dispatch?.Invoke();
                processed++;
            }
        }
    }
}

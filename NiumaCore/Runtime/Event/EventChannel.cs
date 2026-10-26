namespace NiumaCore.Event
{
    /// <summary>
    /// 事件派发信道。
    /// 用于区分必须立即生效的逻辑事件，以及可以延迟到统一时机处理的表现事件。
    /// </summary>
    public enum EventChannel
    {
        /// <summary>
        /// 立即同步派发。
        /// 适合会影响当前逻辑裁决的事件，例如任务状态变更、输入阻塞、关键流程切换。
        /// </summary>
        Immediate = 0,

        /// <summary>
        /// 延迟派发。
        /// 适合 UI 提示、音效、飘字、震屏、粒子、日志等不应拉长当前逻辑链的表现事件。
        /// </summary>
        Deferred = 1
    }
}

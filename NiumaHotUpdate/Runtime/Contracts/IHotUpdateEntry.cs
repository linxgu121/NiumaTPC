namespace NiumaHotUpdate
{
    /// <summary>
    /// AOT 启动层与热更业务程序集之间的稳定入口契约
    /// AOT 层通过该接口启动热更逻辑，不直接引用具体热更类型
    /// </summary>
    public interface IHotUpdateEntry
    {
        /// <summary>
        /// 使用加载器提供的上下文启动热更业务逻辑
        /// </summary>
        void Initialize(HotUpdateEntryContext context);
    }
}

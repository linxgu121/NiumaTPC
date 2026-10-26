namespace NiumaCore.Network
{
    /// <summary>
    /// 网络请求方法。
    /// 只描述语义，不绑定 UnityWebRequest 或具体后端实现。
    /// </summary>
    public enum NetworkHttpMethod
    {
        /// <summary>
        /// 获取资源，不应修改服务器数据。
        /// </summary>
        Get,

        /// <summary>
        /// 创建资源或提交操作，通常带请求体。
        /// </summary>
        Post,

        /// <summary>
        /// 整体更新资源，通常要求客户端提交完整数据。
        /// </summary>
        Put,

        /// <summary>
        /// 局部更新资源，只提交需要修改的字段。
        /// </summary>
        Patch,

        /// <summary>
        /// 删除服务器上的指定资源。
        /// </summary>
        Delete
    }
}

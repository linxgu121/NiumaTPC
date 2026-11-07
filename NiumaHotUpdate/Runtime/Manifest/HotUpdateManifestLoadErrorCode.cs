namespace NiumaHotUpdate
{
    /// <summary>
    /// 热更发布清单加载失败的稳定分类
    /// </summary>
    public enum HotUpdateManifestLoadErrorCode : byte
    {
        /// <summary>
        /// 清单加载、解析和校验全部成功
        /// </summary>
        None = 0,

        /// <summary>
        /// 无法通过资源服务取得远程清单
        /// 具体资源错误保存在 ResourceErrorCode
        /// </summary>
        ResourceLoadFailed = 1,

        /// <summary>
        /// 清单资源存在但文本内容为空
        /// </summary>
        ManifestTextEmpty = 2,

        /// <summary>
        /// 清单文本无法解析为 HotUpdateManifest
        /// </summary>
        ManifestJsonInvalid = 3,

        /// <summary>
        /// 清单结构有效但不适用于当前基础客户端
        /// 具体校验错误保存在 ValidationErrorCode
        /// </summary>
        ManifestValidationFailed = 4
    }
}

using System;

namespace NiumaHotUpdate
{
    /// <summary>
    /// 描述热更程序集的资源地址和内容身份
    /// </summary>
    [Serializable]
    public class HotUpdateAssemblyDescriptor
    {
        /// <summary>
        /// Addressables 资源地址
        /// </summary>
        public string Address;

        /// <summary>
        /// DLL 原始字节的 SHA256
        /// </summary>
        public string Sha256;
    }
}

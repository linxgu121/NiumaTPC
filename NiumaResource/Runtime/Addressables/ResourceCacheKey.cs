using System;

namespace NiumaResource
{
    /// <summary>
    /// 资源缓存字典使用的复合键
    /// 只有地址和请求类型都相同的请求才能共享底层加载
    ///
    /// -------说一些设计与性能优化----------
    /// 注意: 这里实现IEquatable<T>的目的是为了避免装箱减少GC
    /// </summary>
    internal readonly struct ResourceCacheKey : IEquatable<ResourceCacheKey>
    {
        #region Properties

        /// <summary>
        /// 去除首尾空格后的稳定资源地址
        /// 地址比较区分大小写
        /// </summary>
        public string Address { get; }

        /// <summary>
        /// 调用方请求的 Unity 资源类型
        /// </summary>
        public Type AssetType { get; }

        #endregion

        #region Constructor

        /// <summary>
        /// 检查地址和资源类型后创建有效缓存键
        /// 无效数据属于资源服务内部编程错误
        /// </summary>
        public ResourceCacheKey(string address,Type assetType)
        {
            if (string.IsNullOrWhiteSpace(address))
            {
                throw new ArgumentException("资源缓存地址不能为空",nameof(address));
            }

            if (assetType == null)
            {
                throw new ArgumentNullException(nameof(assetType));
            }

            if (!typeof(UnityEngine.Object).IsAssignableFrom(assetType))
            {
                throw new ArgumentException("资源缓存类型必须继承 UnityEngine.Object",nameof(assetType));
            }

            Address = address.Trim();
            AssetType = assetType;
        }

        #endregion

        #region Equality

        /// <summary>
        /// 比较两个缓存键的标准化地址和资源类型
        /// </summary>
        public bool Equals(ResourceCacheKey other)
        {
            return StringComparer.Ordinal.Equals(Address,other.Address) && AssetType == other.AssetType;
        }

        public override bool Equals(object obj)
        {
            return obj is ResourceCacheKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int addressHash = StringComparer.Ordinal.GetHashCode(Address);

                int typeHash = AssetType.GetHashCode();

                return (addressHash * 397) ^ typeHash;
            }
        }

        #endregion

        #region Diagnostics

        public override string ToString()
        {
            return $"{AssetType.FullName}:{Address}";
        }

        #endregion
    }
}

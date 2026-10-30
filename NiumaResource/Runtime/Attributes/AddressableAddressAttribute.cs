using System;
using UnityEngine;

namespace NiumaResource
{
    /// <summary>
    /// 标记一个字符串字段用于保存 Addressables 可读地址
    /// 编辑器将为该字段提供资源选择器
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
    public sealed class AddressableAddressAttribute : PropertyAttribute
    {
        /// <summary>
        /// 该地址允许选择的资源类型
        /// </summary>
        public Type AssetType { get; }

        public AddressableAddressAttribute(Type assetType)
        {
            AssetType = assetType != null && typeof(UnityEngine.Object).IsAssignableFrom(assetType) ? assetType : typeof(UnityEngine.Object);
        }
    }
}
using System;

namespace NiumaCore.Network
{
    /// <summary>
    /// 网络请求头。
    /// 使用结构体减少简单请求构造时的额外对象包装。
    /// </summary>
    [Serializable]
    public readonly struct NetworkHeader
    {
        public readonly string Key;
        public readonly string Value;

        public NetworkHeader(string key, string value)
        {
            Key = key;
            Value = value;
        }
    }
}


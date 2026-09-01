using UnityEngine;

namespace NiumaTPC.Character.Traversal
{
    /// <summary>
    /// 一次翻越环境检测需要的输入
    /// 只保存模拟事实，不依赖Transfrom与FishNet
    /// </summary>
    public readonly struct VaultProbeRequest
    {
        /// <summary>
        /// 发起探测时角色根节点的世界坐标
        /// </summary>
        public readonly Vector3 Position;

        /// <summary>
        /// 发起探测时角色的世界前方
        /// 探测器会忽略 Y 分量并重新归一化。
        /// </summary>
        public readonly Vector3 Forward;

        /// <summary>
        /// 本次允许的最低翻越高度
        /// </summary>
        public readonly float MinHeight;

        /// <summary>
        /// 本次允许的最高翻越高度
        /// </summary>
        public readonly float MaxHeight;

        public VaultProbeRequest(
            Vector3 position,
            Vector3 forward,
            float minHeiht,
            float maxHeight)
        {
            Position = position;
            Forward = forward;
            MinHeight = minHeiht;
            MaxHeight = maxHeight;
        }
    }
}

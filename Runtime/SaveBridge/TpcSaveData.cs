using System;

namespace NiumaTPC.SaveBridge
{
    /// <summary>
    /// TPC 模块写入存档的玩家位姿快照。
    /// 这里只保存可序列化的纯数据，不保存 Transform、GameObject 或控制器引用。
    /// </summary>
    [Serializable]
    public sealed class TpcSaveData
    {
        /// <summary>
        /// 当前场景 ID。第一版使用场景名，后续如果有正式场景表，可以替换为稳定 SceneId。
        /// </summary>
        public string SceneId;

        /// <summary>
        /// 玩家世界坐标 X。
        /// </summary>
        public float PositionX;

        /// <summary>
        /// 玩家世界坐标 Y。
        /// </summary>
        public float PositionY;

        /// <summary>
        /// 玩家世界坐标 Z。
        /// </summary>
        public float PositionZ;

        /// <summary>
        /// 玩家世界旋转四元数 X。
        /// </summary>
        public float RotationX;

        /// <summary>
        /// 玩家世界旋转四元数 Y。
        /// </summary>
        public float RotationY;

        /// <summary>
        /// 玩家世界旋转四元数 Z。
        /// </summary>
        public float RotationZ;

        /// <summary>
        /// 玩家世界旋转四元数 W。
        /// </summary>
        public float RotationW = 1f;
    }
}

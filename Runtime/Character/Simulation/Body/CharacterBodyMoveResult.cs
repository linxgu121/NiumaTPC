using UnityEngine;

namespace NiumaTPC.Character.Simulation
{
    /// <summary>
    /// 场景碰撞身体执行一次移动后的结果。
    /// 不依赖 FishNet，也不向模拟层暴露 Unity CollisionFlags。
    /// </summary>
    public readonly struct CharacterBodyMoveResult
    {
        #region Movement Result(移动请求)

        /// <summary>
        /// 模拟器本 Tick 请求的世界空间位移。
        /// </summary>
        public readonly Vector3 RequestedDisplacement;

        /// <summary>
        /// 碰撞处理后实际产生的世界空间位移。
        /// </summary>
        public readonly Vector3 ActualDisplacement;

        public readonly bool CollidedSides;
        public readonly bool CollidedAbove;
        public readonly bool CollidedBelow;

        #endregion

        #region Constructor(构造)

        public CharacterBodyMoveResult(
            Vector3 requestedDisplacement,
            Vector3 actualDisplacement,
            bool collidedSides,
            bool collidedAbove,
            bool collidedBelow)
        {
            RequestedDisplacement =
                requestedDisplacement;

            ActualDisplacement =
                actualDisplacement;

            CollidedSides = collidedSides;
            CollidedAbove = collidedAbove;
            CollidedBelow = collidedBelow;
        }

        #endregion
    }
}

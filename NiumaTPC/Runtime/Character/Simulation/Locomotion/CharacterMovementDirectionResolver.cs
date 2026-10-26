using UnityEngine;

namespace NiumaTPC.Character.Simulation
{
    /// <summary>
    /// 把二维移动输入转换成基于视角朝向的世界移动方向。
    /// 不读取 Camera、Transform 或 Time。
    /// </summary>
    public static class CharacterMovementDirectionResolver
    {
        #region Constants(常量)

        private const float MoveInputThresholdSquared = 0.01f;

        #endregion

        #region Public API
        /// <summary>
        /// 尝试计算当前 Tick 的世界移动方向和目标朝向
        /// </summary>
        /// <returns>存在有效移动输入时返回 true</returns>
        public static bool TryResolve(in CharacterInputCommand command, out Vector3 worldDirection, out float targetYaw)
        {
            Vector2 move = Vector2.ClampMagnitude(command.Move, 1f);

            if(move.sqrMagnitude <= MoveInputThresholdSquared)
            {
                worldDirection = Vector3.zero;
                targetYaw = Mathf.Repeat(command.ViewYaw, 360f);
                return false;
            }

            float viewYaw = Mathf.Repeat(command.ViewYaw, 360f);

            Quaternion viewRotation = Quaternion.Euler(0f, viewYaw, 0f);

            Vector3 localDirection = new Vector3(move.x, 0f, move.y);

            worldDirection = viewRotation * localDirection;
            worldDirection.y = 0f;
            worldDirection.Normalize();

            targetYaw = Mathf.Atan2(worldDirection.x, worldDirection.z) * Mathf.Rad2Deg;

            targetYaw = Mathf.Repeat(targetYaw, 360f);

            return true;
        }

        #endregion
        
    }
}
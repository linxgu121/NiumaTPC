using System;
using UnityEngine;

namespace NiumaTPC.Character.Simulation
{
    /// <summary>
    /// 使用 Unity CharacterController 执行模拟位移。
    /// 负责碰撞、坡度、台阶以及权威姿态覆盖
    /// </summary>
    public sealed class CharacterControllerSimulationBody : ICharacterSimulationBody
    {
        #region Dependencies(依赖)

        private readonly CharacterController _controller;

        private readonly Transform _transform;

        #endregion

        #region Properties

        public Vector3 Position => _transform.position;

        public float Yaw => Mathf.Repeat(_transform.eulerAngles.y, 360f);

        public bool IsGrounded => _controller.isGrounded;

        #endregion

        #region Constructor(构造函数)

        public CharacterControllerSimulationBody(CharacterController controller)
        {
            if(controller == null)
            {
                throw new ArgumentNullException(nameof(controller), "角色模拟身体需要有效的 CharacterController。");

            }

            _controller = controller;
            _transform = controller.transform;
        }

        #endregion

        #region Public API

        public CharacterBodyMoveResult Move(Vector3 displacement)
        {
            ValidateFiniteVector(displacement, nameof(displacement));

            if (!_controller.enabled)
            {
                throw new InvalidOperationException("CharacterController 已禁用，无法执行模拟位移。");
            }

            Vector3 startPosition = _transform.position;

            CollisionFlags collisionFlags = _controller.Move(displacement);

            Vector3 actualDisplacement = _transform.position - startPosition;

            return new CharacterBodyMoveResult(
                displacement,
                actualDisplacement,
                collidedSides: (collisionFlags & CollisionFlags.Sides) != 0,
                collidedAbove: (collisionFlags & CollisionFlags.Above) != 0,
                collidedBelow: (collisionFlags & CollisionFlags.Below) != 0);
            
        }

        public void SetYaw(float yaw)
        {
            float normalizedYaw = NormalizeYaw(yaw);

            _transform.rotation = Quaternion.Euler(0f, normalizedYaw, 0f);
        }

        public void SetPose(Vector3 position, float yaw)
        {
            ValidateFiniteVector(position, nameof(position));

            float normalizedYaw = NormalizeYaw(yaw);
            bool wasEnabled = _controller.enabled;

            // 权威校正不是正常移动，临时关闭控制器，
            // 避免位置覆盖被碰撞系统阻止。
            if (wasEnabled)
            {
                _controller.enabled = false;
            }

            try
            {
                _transform.SetPositionAndRotation(
                    position,
                    Quaternion.Euler(
                        0f,
                        normalizedYaw,
                        0f));
            }
            finally
            {
                if (wasEnabled)
                {
                    _controller.enabled = true;
                }
            }
        }

        #endregion

        #region Validation(校验)

        private static float NormalizeYaw(float yaw)
        {
            if(float.IsNaN(yaw) || float.IsInfinity(yaw))
            {
                throw new ArgumentOutOfRangeException(nameof(yaw),yaw,"角色 Yaw 必须是有限数值。");

            }

            return Mathf.Repeat(yaw, 360f);
        }

        private static void ValidateFiniteVector(Vector3 value, string parameterName)
        {
            bool isInvalid = float.IsNaN(value.x) || float.IsNaN(value.y) || float.IsNaN(value.z) ||
                float.IsInfinity(value.x) || float.IsInfinity(value.y) || float.IsInfinity(value.z);

            if (isInvalid)
            {
                throw new ArgumentOutOfRangeException(parameterName,value,"角色位置或位移不能包含 NaN 或 Infinity。");
            }

        }

        #endregion

    }
}
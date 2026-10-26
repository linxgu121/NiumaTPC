using System;
using NiumaTPC.Character.Config.PlayerSOModules;
using NiumaTPC.Character.RuntimeData;
using UnityEngine;

namespace NiumaTPC.Character.Traversal
{
    /// <summary>
    /// 使用 Unity Physics 执行翻越环境探测
    /// 本类只负责查询环境，不决定角色是否进入 Vault 状态
    /// </summary>
    public sealed class PhysicsCharacterTraversalProbe : ICharacterTraversalProbe
    {
        #region Dependencies(依赖)

        private readonly VaultingSO _config;

        /// <summary>
        /// CharacterController 胶囊脚底相对于角色根节点的偏移
        /// 已包含角色根节点缩放，但不包含运行时 Yaw
        /// </summary>
        private readonly Vector3 _scaledLocalFootOffset;

        #endregion

        #region Runtime State

        private bool _warnedMissingObstacleMask;
        private bool _warnedMissingGroundMask;

        #endregion

        #region Constructor(构造器)

         public PhysicsCharacterTraversalProbe(VaultingSO config, CharacterController characterController)
        {
            _config = config != null ? config : throw new ArgumentNullException(
                nameof(config),
                "翻越环境探测器需要有效的 VaultingSO。");

            if (characterController == null)
            {
                throw new ArgumentNullException(nameof(characterController),"翻越环境探测器需要有效的 CharacterController。");
            }

            /*
             * CharacterController.center 和 height 都位于角色本地空间。
             * 胶囊脚底 = 胶囊中心 - 半高。
             */
            Vector3 localFootPoint = characterController.center + Vector3.down * (characterController.height * 0.5f);

            /*
             * TransformVector 将根节点缩放计入偏移；
             * 随后移除当前旋转，保存为稳定的根节点局部偏移。
             */
            Vector3 worldFootOffset = characterController.transform.TransformVector(localFootPoint);

            _scaledLocalFootOffset =Quaternion.Inverse(characterController.transform.rotation) * worldFootOffset;
        }

        #endregion

        #region Public API

        public bool TryProbeVault(
            in VaultProbeRequest request,
            out VaultObstacleInfo obstacleInfo)
        {
            obstacleInfo = new VaultObstacleInfo
            {
                IsValid = false
            };

            if (!TryNormalizeRequest(in request, out Vector3 forward))
            {
                return false;
            }

            Vector3 footPosition = ResolveFootPosition(request.Position,forward);

            int obstacleMask = ResolveObstacleMask();
            int groundMask = ResolveGroundMask();

            if (!TryFindWall(
                    footPosition,
                    forward,
                    request.MinHeight,
                    request.MaxHeight,
                    obstacleMask,
                    out RaycastHit wallHit))
            {
                return false;
            }

            // 上向法线通常代表地面或缓坡，不作为翻越墙面。
            if (Vector3.Dot(wallHit.normal, Vector3.up) > 0.1f)
            {
                return false;
            }

            Vector3 downRayStart =
                wallHit.point +
                Vector3.up * _config.VaultDownwardRayLength +
                forward * _config.VaultDownwardRayOffset;

            if (!Physics.Raycast(
                    downRayStart,
                    Vector3.down,
                    out RaycastHit ledgeHit,
                    _config.VaultDownwardRayLength,
                    groundMask,
                    QueryTriggerInteraction.Ignore))
            {
                return false;
            }

            // 墙顶必须接近水平，才能视为有效翻越平面。
            if (Vector3.Dot(ledgeHit.normal, Vector3.up) < 0.9f)
            {
                return false;
            }

            // Low/High 高度统一相对于胶囊脚底计算。
            float height = ledgeHit.point.y - footPosition.y;

            if (height < request.MinHeight ||
                height > request.MaxHeight)
            {
                return false;
            }

            Vector3 wallNormalFlat = new Vector3(
                wallHit.normal.x,
                0f,
                wallHit.normal.z);

            if (wallNormalFlat.sqrMagnitude < 0.0001f)
            {
                return false;
            }

            wallNormalFlat.Normalize();

            Vector3 vaultForwardDirection =
                -wallNormalFlat;

            float landForwardDistance =
                _config.VaultLandDistance +
                Mathf.Max(
                    0f,
                    _config.VaultLandForwardPadding);

            /*
             * ledgeHit.point 才是下向射线实际命中的墙顶点。
             * wallHit.point 位于竖直墙面，不能作为胶囊脚底的墙顶目标。
             */
            Vector3 ledgeSurfacePoint =
                ledgeHit.point;

            /*
             * 落脚距离必须从墙体前沿计算，不能从 ledgeSurfacePoint 继续累加。
             * ledgeSurfacePoint 本身已经包含 VaultDownwardRayOffset；如果再次把它
             * 当作落脚搜索起点，会把这段偏移重复计算，并可能直接跳到方块另一侧。
             * 顶面在短距离内足够站立时，射线会命中顶面，角色就停在方块顶部。
             */
            Vector3 wallTopFrontPoint = new Vector3(
                wallHit.point.x,
                ledgeSurfacePoint.y,
                wallHit.point.z);

            Vector3 landRayStart =
                wallTopFrontPoint +
                vaultForwardDirection *
                landForwardDistance +
                Vector3.up * 0.5f;

            bool foundGround = Physics.Raycast(
                landRayStart,
                Vector3.down,
                out RaycastHit landHit,
                _config.VaultLandRayLength,
                groundMask,
                QueryTriggerInteraction.Ignore) &&
                Vector3.Dot(
                    landHit.normal,
                    Vector3.up) >= 0.7f;

            if (_config.RequireGroundBehindWall &&
                !foundGround)
            {
                return false;
            }

            Vector3 expectedLandSurfacePoint =
                foundGround
                    ? landHit.point
                    : landRayStart +
                      Vector3.down * 0.5f;

            /*
             * Physics 命中点表示胶囊脚底应该到达的表面位置，
             * CharacterSimulationState 保存的却是角色根节点位置。
             * foot = root + rotation * footOffset，
             * 因此 root = foot - rotation * footOffset。
             */
            Quaternion targetRootRotation =
                Quaternion.LookRotation(
                    vaultForwardDirection,
                    Vector3.up);

            Vector3 targetRootOffsetFromFoot =
                -(targetRootRotation *
                  _scaledLocalFootOffset);

            Vector3 ledgeRootTarget =
                ledgeSurfacePoint +
                targetRootOffsetFromFoot;

            Vector3 expectedLandRootTarget =
                expectedLandSurfacePoint +
                targetRootOffsetFromFoot;

            Vector3 rightDirection =
                Vector3.Cross(Vector3.up, wallNormalFlat).normalized;

            // 不再依赖 Transform.right，直接由本次权威朝向计算角色右方。
            Vector3 characterRight =
                Vector3.Cross(Vector3.up, forward).normalized;

            if (characterRight.sqrMagnitude > 0.0001f &&
                Vector3.Dot(rightDirection, characterRight) < 0f)
            {
                rightDirection = -rightDirection;
            }

            float halfHandSpread = _config.VaultHandSpread * 0.5f;

            Vector3 baseLeftHand =
                ledgeSurfacePoint -
                rightDirection * halfHandSpread;

            Vector3 baseRightHand =
                ledgeSurfacePoint +
                rightDirection * halfHandSpread;

            Quaternion ledgeBasis = Quaternion.LookRotation(
                vaultForwardDirection,
                Vector3.up);

            Vector3 leftHandPosition =
                baseLeftHand +
                ledgeBasis * _config.LeftHandIKOffset;

            Vector3 rightHandPosition =
                baseRightHand +
                ledgeBasis * _config.RightHandIKOffset;

            Quaternion baseHandRotation = Quaternion.LookRotation(
                -wallNormalFlat,
                Vector3.up);

            Quaternion handRotation =
                baseHandRotation *
                Quaternion.Euler(_config.HandRotationOffsetEuler);

            obstacleInfo = new VaultObstacleInfo
            {
                IsValid = true,
                WallPoint = wallHit.point,
                WallNormal = wallNormalFlat,
                Height = height,
                LedgePoint = ledgeRootTarget,
                ExpectedLandPoint = expectedLandRootTarget,
                LeftHandPos = leftHandPosition,
                RightHandPos = rightHandPosition,
                HandRot = handRotation
            };

            return true;
        }

        #endregion

        #region Probe Origin(探测原点)

        /// <summary>
        /// 使用权威根节点位置和朝向还原 CharacterController 世界脚底。
        /// 不读取动画模型或 Graphical Object。
        /// </summary>
        private Vector3 ResolveFootPosition(Vector3 rootPosition,Vector3 forward)
        {
            Quaternion rootYawRotation = Quaternion.LookRotation(forward,Vector3.up);

            return rootPosition + rootYawRotation * _scaledLocalFootOffset;
        }

        #endregion

        #region Wall Detection(墙检测)

        /// <summary>
        /// 寻找墙体
        /// 在 minHeight ~ maxHeight高度区间内，
        /// 发射 3 条水平高度不同的射线，只要任意一条命中竖直墙体
        /// 就判定前方存在可翻越的墙，输出碰撞信息
        /// </summary>
        private bool TryFindWall(
            Vector3 position,
            Vector3 forward,
            float minHeight,
            float maxHeight,
            int obstacleMask,
            out RaycastHit wallHit)
        {
            float configuredHeight =
                Mathf.Max(0.05f, _config.VaultForwardRayHeight);

            if (TryRaycastWallAtHeight(
                    position,
                    forward,
                    configuredHeight,
                    obstacleMask,
                    out wallHit))
            {
                return true;
            }

            float maximumRayHeight = Mathf.Max(0.05f, maxHeight - 0.05f);

            float middleHeight = Mathf.Clamp(
                (minHeight + maxHeight) * 0.5f,
                0.05f,
                maximumRayHeight);

            if (!Mathf.Approximately(
                    middleHeight,
                    configuredHeight) &&
                TryRaycastWallAtHeight(
                    position,
                    forward,
                    middleHeight,
                    obstacleMask,
                    out wallHit))
            {
                return true;
            }

            float lowHeight = Mathf.Clamp(
                minHeight + 0.1f,
                0.05f,
                maximumRayHeight);

            if (!Mathf.Approximately(
                    lowHeight,
                    configuredHeight) &&
                !Mathf.Approximately(
                    lowHeight,
                    middleHeight) &&
                TryRaycastWallAtHeight(
                    position,
                    forward,
                    lowHeight,
                    obstacleMask,
                    out wallHit))
            {
                return true;
            }

            wallHit = default;
            return false;
        }

        /// <summary>
        /// 在指定高度水平向前发射射线，检测是不是竖直墙体
        /// </summary>
        private bool TryRaycastWallAtHeight(
            Vector3 position,
            Vector3 forward,
            float height,
            int obstacleMask,
            out RaycastHit wallHit)
        {
            Vector3 rayStart = position + Vector3.up * height;

            if (!Physics.Raycast(
                    rayStart,
                    forward,
                    out wallHit,
                    _config.VaultForwardRayLength,
                    obstacleMask,
                    QueryTriggerInteraction.Ignore))
            {
                return false;
            }

            return Vector3.Dot(wallHit.normal,Vector3.up) <= 0.1f;
        }

        #endregion

        #region Layer Masks(层级)

        /// <summary>
        /// 返回翻越探测障碍物层级掩码
        /// </summary>
        private int ResolveObstacleMask()
        {
            int mask = _config.ObstacleLayers.value;

            if (mask != 0)
            {
                return mask;
            }

            if (!_warnedMissingObstacleMask)
            {
                Debug.LogWarning(
                    "[PhysicsCharacterTraversalProbe] " +
                    "VaultingSO.ObstacleLayers 未配置，" +
                    "临时使用 Physics.DefaultRaycastLayers。");

                _warnedMissingObstacleMask = true;
            }

            return Physics.DefaultRaycastLayers;
        }

        /// <summary>
        /// 返回地面检测层级掩码
        /// </summary>
        private int ResolveGroundMask()
        {
            int mask = _config.GroundLayers.value;

            if (mask != 0)
            {
                return mask;
            }

            if (!_warnedMissingGroundMask)
            {
                Debug.LogWarning(
                    "[PhysicsCharacterTraversalProbe] " +
                    "VaultingSO.GroundLayers 未配置，" +
                    "临时使用 Physics.DefaultRaycastLayers。");

                _warnedMissingGroundMask = true;
            }

            return Physics.DefaultRaycastLayers;
        }

        #endregion

        #region Validation(校验)

        /// <summary>
        /// 返回bool代表探测请求是否合法，可以执行翻越探测
        /// </summary>
        private static bool TryNormalizeRequest(
            in VaultProbeRequest request,
            out Vector3 forward)
        {
            forward = request.Forward;
            forward.y = 0f;

            bool hasInvalidValue =
                !IsFinite(request.Position.x) ||
                !IsFinite(request.Position.y) ||
                !IsFinite(request.Position.z) ||
                !IsFinite(forward.x) ||
                !IsFinite(forward.z) ||
                !IsFinite(request.MinHeight) ||
                !IsFinite(request.MaxHeight);

            if (hasInvalidValue ||
                request.MinHeight < 0f ||
                request.MaxHeight < request.MinHeight ||
                forward.sqrMagnitude < 0.0001f)
            {
                return false;
            }

            forward.Normalize();
            return true;
        }

        /// <summary>
        /// 校验浮点数：排除NaN、±Infinity
        /// </summary>
        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) &&
                   !float.IsInfinity(value);
        }

        #endregion
    }
}

using System;
using FishNet.Connection;
using FishNet.Object;
using NiumaTPC.Cameras;
using NiumaTPC.Character;
using UnityEngine;

namespace NiumaTPC.FishNet
{
    /// <summary>
    /// 根据 FishNet Owner，把场景中唯一的摄像机系统
    /// 绑定到当前客户端的本地玩家。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NiumaCharacterController))]
    public sealed class NiumaFishNetCameraBinder: NetworkBehaviour
    {
        #region Inspector(组件获取)

        [SerializeField]
        [Tooltip("当前网络角色:为空时自动获取同物体上的NiumaCharacterController")]
        private NiumaCharacterController _player;

        #endregion

        #region Runtime State(运行时)
        private PlayerCameraManager _cameraManager;
        private CameraRigDriver _cameraRigDriver;
        private bool _cameraBound;

        #endregion

        #region Unity Lifecycle(Unity生命周期)
        private void Awake()
        {
            if(_player == null)
            {
                _player = GetComponent<NiumaCharacterController>();
            }
        }

        #endregion

        #region FishNet Lifecycle(FishNet网络同步生命周期)

        public override void OnStartClient()
        {
            RefreshCameraOwnership();
        }

        public override void OnStopClient()
        {
            ReleaseCamera();
        }

        public override void OnOwnershipClient(NetworkConnection prevOwner)
        {
            RefreshCameraOwnership();
        }

        #endregion

        #region Camera Ownership(相机所有权)

        private void RefreshCameraOwnership()
        {
            ReleaseCamera();

            // 远端副本没有资格控制本客户端的摄像机。
            if (_player == null || !Owner.IsLocalClient)
            {
                return;
            }

            _cameraManager = FindFirstObjectByType<PlayerCameraManager>();

            _cameraRigDriver = FindFirstObjectByType<CameraRigDriver>();

            if (_cameraManager == null || _cameraRigDriver == null)
            {
                Debug.LogError(
                    "[NiumaFishNet摄像机] 场景中缺少 " +
                    "PlayerCameraManager 或 CameraRigDriver。",
                    this);

                _cameraManager = null;
                _cameraRigDriver = null;
                return;
            }

            _cameraManager.BindPlayer(_player);
            _cameraRigDriver.BindPlayer(_player);
            _cameraBound = true;

            Debug.Log(
                $"[NiumaFishNet摄像机] 已绑定本地玩家：" +
                $"ObjectId={ObjectId}, OwnerId={OwnerId}",
                this);
        }

        /// <summary>
        /// 解除对角色的绑定
        /// </summary>
        private void ReleaseCamera()
        {
            if (!_cameraBound)
            {
                return;
            }

            if (_cameraManager != null)
            {
                _cameraManager.UnbindPlayer(_player);
            }

            if (_cameraRigDriver != null)
            {
                _cameraRigDriver.UnbindPlayer(_player);
            }

            _cameraManager = null;
            _cameraRigDriver = null;
            _cameraBound = false;
        }

        #endregion

    }
}

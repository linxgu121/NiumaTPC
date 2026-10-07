using FishNet.Connection;
using FishNet.Object;
using NiumaTPC.Character;
using NiumaTPC.Item;
using UnityEngine;

namespace NiumaTPC.FishNet
{
    /// <summary>
    /// 武器请求的 FishNet 适配入口
    /// 当前阶段只验证请求与接收回执，不执行射击
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NiumaCharacterController))]
    public sealed class NiumaFishNetWeaponDriver : NetworkBehaviour, INetworkWeaponFireRequestSender
    {
        #region Inspector

        [SerializeField]
        [Tooltip("绑定同根节点的 OfflineWeaponFireSource，本步只复用它采样请求；为空时获取同物体组件")]
        private OfflineWeaponFireSource _requestSource;

        [SerializeField]
        [Tooltip("打印请求提交、服务器接收与拥有者回执；接入连续开火后建议关闭")]
        private bool _logRequests = true;

        #endregion

        #region 运行时状态

        private NiumaCharacterController _player;

        // 只在服务器侧使用，不与客户端的请求生成计数共用
        private ulong _lastReceivedRequestSequence;

        // 只由服务器装备流程确认，不从客户端表现状态自动复制
        private ItemInstance _serverEquippedItem;

        // 0 表示尚未发生装备确认变更
        private uint _serverEquipmentRevision;

        // 保存服务器创建的出生装备实例
        // 后续切换装备时复用，不能重新创建并重置弹量
        private ItemInstance[] _serverInitialEquipment;

        // 只限制正常客户端的请求发送频率，不是服务器武器冷却
        private double _nextFireSubmissionTime;

        // 服务端记录：Owner 已完成客户端对象初始化并请求快照
        private bool _serverOwnerReady;

        // 客户端记录：只缓存服务器发来的装备身份
        private bool _hasOwnerEquipmentSnapshot;
        private uint _ownerEquipmentRevision;
        private string _ownerEquipmentInstanceId;
        private string _ownerEquipmentDefinitionId;

        // 当前服务器装备对应的出生槽位，-1 表示无有效槽位
        private int _ownerEquipmentSlotIndex = -1;

        #endregion

        #region 生命周期

        private void Awake()
        {
            _player = GetComponent<NiumaCharacterController>();

            if (_requestSource == null)
            {
                _requestSource = GetComponent<OfflineWeaponFireSource>();
            }
        }

        public override void OnStartServer()
        {
            _lastReceivedRequestSequence = 0UL;

            _serverEquippedItem = null;
            _serverEquipmentRevision = 0u;
            _serverOwnerReady = false;
            _serverInitialEquipment = null;

            InitializeServerEquipment();
        }

        public override void OnStopServer()
        {
            // 释放当前网络对象持有的服务器物品引用
            // 不修改客户端表现实例，也不修改共享配置
            _serverEquippedItem = null;
            _serverInitialEquipment = null;

            _serverEquipmentRevision = 0u;
            _lastReceivedRequestSequence = 0UL;
            _serverOwnerReady = false;
        }
        public override void OnStartClient()
        {
            _nextFireSubmissionTime = 0d;
            ClearOwnerEquipmentSnapshot();

            if (IsOwner)
            {
                // 客户端对象已经初始化，再请求服务器当前记录
                RequestEquipmentSnapshotServerRpc();
            }
        }

        public override void OnStopClient()
        {
            ClearOwnerEquipmentSnapshot();
        }

        #endregion

        #region 服务器装备记录

        /// <summary>
        /// 根据服务器角色的出生配置创建独立物品实例
        /// 不读取客户端当前手持物，也不生成武器模型
        /// </summary>
        private void InitializeServerEquipment()
        {
            if (!IsServerInitialized || _serverInitialEquipment != null)
            {
                return;
            }

            if (_player == null)
            {
                Debug.LogError("[网络装备] 初始化失败，缺少角色组件", this);
                return;
            }

            // 复用角色现有出生配置，不增加第二套 Inspector 配置
            EquippableItemSO[] definitions = { _player.DefaultEquipment1, _player.DefaultEquipment2, _player.DefaultEquipment3 };

            _serverInitialEquipment = new ItemInstance[definitions.Length];

            ItemInstance firstItem = null;

            for (int i = 0; i < definitions.Length; i++)
            {
                EquippableItemSO definition = definitions[i];

                if (definition == null)
                {
                    // 保留空槽位置，不把后面的装备向前挤
                    continue;
                }

                // 每件装备拥有独立实例及武器运行时状态
                ItemInstance instance = new ItemInstance(definition, 1);
                _serverInitialEquipment[i] = instance;

                if (firstItem == null)
                {
                    firstItem = instance;
                }
            }

            // 默认手持第一件非空装备
            // 所有配置均为空时，明确确认空手
            TrySetServerEquipment(firstItem);
        }

        /// <summary>
        /// 接收服务器装备流程已经确认的物品实例
        /// 这是服务器内部调用入口，不是客户端可调用的 RPC
        /// </summary>
        public bool TrySetServerEquipment(ItemInstance confirmedItem)
        {
            if (!IsServerInitialized)
            {
                return false;
            }

            // null 表示确认空手
            // 非空实例必须仍然存在，并且属于可装备物品
            if (confirmedItem != null &&
                (confirmedItem.CurrentAmount <= 0 ||
                 !(confirmedItem.BaseData is EquippableItemSO)))
            {
                return false;
            }

            // 当前阶段只允许确认服务器实际创建的出生装备
            if (confirmedItem != null && FindServerEquipmentSlot(confirmedItem) < 0)
            {
                return false;
            }

            // 已经完成过首次确认，才把相同引用视为重复确认
            // 首次确认空手也需要将代次从 0 推进到 1
            if (_serverEquipmentRevision != 0u && ReferenceEquals(_serverEquippedItem, confirmedItem))
            {
                SendEquipmentSnapshotToOwner();
                return true;
            }

            // 先计算成功，再一起提交
            // 不允许溢出后重新使用旧代次
            uint nextRevision = checked(_serverEquipmentRevision + 1u);

            _serverEquippedItem = confirmedItem;
            _serverEquipmentRevision = nextRevision;

            if (_logRequests)
            {
                string itemId = confirmedItem != null
                    ? confirmedItem.BaseData.ItemID
                    : "空手";

                Debug.Log(
                    $"[网络装备/服务器确认] ObjectId={ObjectId}, " +
                    $"ItemId={itemId}, " +
                    $"EquipmentRevision={_serverEquipmentRevision}",
                    this);
            }

            SendEquipmentSnapshotToOwner();

            return true;
        }

        /// <summary>
        /// 查找服务器持有的出生装备实例
        /// 不按配置 ID 查找，避免混淆两件同型号物品
        /// </summary>
        private int FindServerEquipmentSlot(ItemInstance item)
        {
            if (item == null || _serverInitialEquipment == null)
            {
                return -1;
            }

            for (int i = 0; i < _serverInitialEquipment.Length; i++)
            {
                if (ReferenceEquals(_serverInitialEquipment[i], item))
                {
                    return i;
                }
            }

            return -1;
        }

        #endregion

        #region 装备快照同步

        /// <summary>
        /// Owner 只请求当前记录，不提交物品或装备代次
        /// </summary>
        [ServerRpc(RequireOwnership = true, RunLocally = false)]
        private void RequestEquipmentSnapshotServerRpc(NetworkConnection sender = null)
        {
            if (!IsServerInitialized ||
                sender == null ||
                !sender.IsActive ||
                sender != Owner)
            {
                return;
            }

            _serverOwnerReady = true;
            SendEquipmentSnapshotToOwner();
        }

        /// <summary>
        /// 将服务器当前确认的装备身份发送给拥有者
        /// </summary>
        private void SendEquipmentSnapshotToOwner()
        {
            if (!IsServerInitialized ||
                !_serverOwnerReady ||
                Owner == null ||
                !Owner.IsActive)
            {
                return;
            }

            string instanceId = _serverEquippedItem != null
                ? _serverEquippedItem.InstanceID
                : string.Empty;

            string definitionId = _serverEquippedItem != null
                ? _serverEquippedItem.BaseData.ItemID
                : string.Empty;

            TargetReceiveEquipmentSnapshot(
                Owner,
                _serverEquipmentRevision,
                instanceId,
                definitionId,
                FindServerEquipmentSlot(_serverEquippedItem));
        }

        /// <summary>
        /// 只更新本地服务器快照，不直接驱动模型换装
        /// </summary>
        [TargetRpc(RunLocally = false)]
        private void TargetReceiveEquipmentSnapshot(
            NetworkConnection target,
            uint equipmentRevision,
            string instanceId,
            string definitionId,
            int slotIndex)
        {
            if (!IsClientInitialized || !IsOwner)
            {
                return;
            }

            // 防止较旧快照覆盖已经收到的新装备记录
            if (_hasOwnerEquipmentSnapshot &&
                equipmentRevision < _ownerEquipmentRevision)
            {
                return;
            }

            _hasOwnerEquipmentSnapshot = true;
            _ownerEquipmentRevision = equipmentRevision;
            _ownerEquipmentInstanceId = instanceId ?? string.Empty;
            _ownerEquipmentSlotIndex = slotIndex;
            _ownerEquipmentDefinitionId = definitionId ?? string.Empty;

            if (_logRequests)
            {
                string state = equipmentRevision == 0u
                    ? "尚未确认"
                    : string.IsNullOrEmpty(_ownerEquipmentInstanceId)
                        ? "空手"
                        : "已确认装备";

                Debug.Log(
                    $"[网络装备/Owner快照] ObjectId={ObjectId}, " +
                    $"Revision={_ownerEquipmentRevision}, " +
                    $"Slot={_ownerEquipmentSlotIndex}, " +
                    $"InstanceId={_ownerEquipmentInstanceId}, " +
                    $"ItemId={_ownerEquipmentDefinitionId}, " +
                    $"State={state}",
                    this);
            }
        }

        private void ClearOwnerEquipmentSnapshot()
        {
            _hasOwnerEquipmentSnapshot = false;
            _ownerEquipmentRevision = 0u;
            _ownerEquipmentInstanceId = string.Empty;
            _ownerEquipmentDefinitionId = string.Empty;
            _ownerEquipmentSlotIndex = -1;
        }

        #endregion

        #region 本地装备对应

        /// <summary>
        /// 根据服务器出生槽位，核对当前本地手持实例
        /// 只解析服务器确认代次，不执行射击或切换装备
        /// </summary>
        private bool TryGetConfirmedEquipmentRevision(
            ItemInstance localItem,
            out uint serverEquipmentRevision)
        {
            serverEquipmentRevision = 0u;

            if (!IsClientInitialized ||
                !IsOwner ||
                !_hasOwnerEquipmentSnapshot ||
                _ownerEquipmentRevision == 0u ||
                _ownerEquipmentSlotIndex < 0 ||
                string.IsNullOrEmpty(_ownerEquipmentInstanceId) ||
                string.IsNullOrEmpty(_ownerEquipmentDefinitionId))
            {
                return false;
            }

            if (localItem == null ||
                localItem.BaseData == null ||
                _player == null ||
                _player.RuntimeData == null ||
                _player.InventoryController == null ||
                !ReferenceEquals(_player.RuntimeData.CurrentItem, localItem))
            {
                return false;
            }

            var hotbar = _player.InventoryController.HotbarInventory;

            if (hotbar == null)
            {
                return false;
            }

            ItemInstance slotItem = hotbar.GetAt(_ownerEquipmentSlotIndex);

            // 必须是对应槽位的同一个本地实例
            // 仅仅枪械型号相同，不代表是同一件物品
            if (!ReferenceEquals(slotItem, localItem) ||
                localItem.BaseData.ItemID != _ownerEquipmentDefinitionId)
            {
                return false;
            }

            serverEquipmentRevision = _ownerEquipmentRevision;
            return true;
        }

        #endregion

        #region 客户端提交

        /// <summary>
        /// 从当前枪械接收开火意图
        /// 只负责本地检查、采样和发送，不执行权威射击
        /// </summary>
        public bool TrySubmitFire(
            ItemInstance weaponInstance,
            uint equipmentRevision)
        {
            if (!isActiveAndEnabled ||
                !IsClientInitialized ||
                !IsOwner ||
                _player == null ||
                !_player.isActiveAndEnabled ||
                _player.RuntimeData == null ||
                _requestSource == null)
            {
                return false;
            }

            var data = _player.RuntimeData;

            // 本地预检查只用于减少无效请求
            // 服务器之后仍须根据自己的状态重新检查
            if (data.IsDead ||
                data.Arbitration.IsDead ||
                data.Arbitration.BlockInput ||
                data.Arbitration.BlockUpperBody ||
                !data.IsAiming)
            {
                return false;
            }

            if (weaponInstance == null ||
                weaponInstance.CurrentAmount <= 0 ||
                data.CurrentItem != weaponInstance ||
                equipmentRevision == 0u ||
                data.EquipmentRevision != equipmentRevision)
            {
                return false;
            }

            RangedWeaponSO weapon = weaponInstance.BaseData as RangedWeaponSO;

            if (weapon == null)
            {
                return false;
            }

            if (!TryGetConfirmedEquipmentRevision(weaponInstance, out uint serverEquipmentRevision))
            {
                return false;
            }

            double now = Time.timeAsDouble;

            // 不允许按住开火时每个渲染帧都发送 RPC
            if (now < _nextFireSubmissionTime)
            {
                return false;
            }

            // 已通过 Owner 检查，只读取本客户端的主相机
            Camera localCamera = Camera.main;

            if (localCamera == null)
            {
                return false;
            }

            _requestSource.SetAimCamera(localCamera);

            // 网络请求携带服务器确认代次，不使用本地表现代次
            if (!_requestSource.TryCreateRequest(
                    serverEquipmentRevision,
                    out WeaponFireRequest request))
            {
                return false;
            }

            uint clientTick = TimeManager.LocalTick;

            if (!TrySendFireRequest(request, clientTick))
            {
                return false;
            }

            // 只推进发送节流，不扣弹、不修改武器权威冷却
            _nextFireSubmissionTime = now + Mathf.Max(0.001f, weapon.FireRate);

            return true;
        }

        /// <summary>
        /// true 只表示本地已经提交 RPC，不代表服务器接受了射击
        /// 重发同一次请求时，应保留原请求和原 clientTick
        /// </summary>
        private bool TrySendFireRequest(
            WeaponFireRequest request,
            uint clientTick)
        {
            if (!isActiveAndEnabled ||
                !IsClientInitialized ||
                !IsOwner)
            {
                return false;
            }

            if (_logRequests)
            {
                Debug.Log(
                    $"[网络射击/提交] ObjectId={ObjectId}, " +
                    $"Request={request.RequestSequence}, " +
                    $"EquipmentRevision={request.EquipmentRevision}, " +
                    $"ClientTick={clientTick}",
                    this);
            }

            SubmitFireServerRpc(request, clientTick);
            return true;
        }

        #endregion

        #region 服务器接收

        [ServerRpc(RequireOwnership = true, RunLocally = false)]
        private void SubmitFireServerRpc(
            WeaponFireRequest request,
            uint clientTick,
            NetworkConnection sender = null)
        {
            // sender 由 FishNet 注入，不接受客户端自行指定射手
            if (!IsServerInitialized ||
                sender == null ||
                !sender.IsActive ||
                sender != Owner)
            {
                return;
            }

            bool isNewRequest =
                request.RequestSequence != 0UL &&
                request.RequestSequence > _lastReceivedRequestSequence;

            if (isNewRequest)
            {
                // 即使瞄准数据无效，也消费这个新编号
                _lastReceivedRequestSequence = request.RequestSequence;
            }

            bool validAim = request.TryGetAimRay(out _);
            uint serverTick = TimeManager.Tick;

            if (_logRequests)
            {
                Debug.Log(
                    $"[网络射击/服务器收到] Sender={sender.ClientId}, " +
                    $"ObjectId={ObjectId}, " +
                    $"Request={request.RequestSequence}, " +
                    $"ClientTick={clientTick}, ServerTick={serverTick}, " +
                    $"NewRequest={isNewRequest}, ValidAim={validAim}",
                    this);
            }

            // 本步只回执接收情况，不调用离线 TryFire
            TargetReceiveRequestReceipt(
                sender,
                request.RequestSequence,
                serverTick,
                isNewRequest,
                validAim);
        }

        #endregion

        #region 拥有者回执

        [TargetRpc(RunLocally = false)]
        private void TargetReceiveRequestReceipt(
            NetworkConnection target,
            ulong requestSequence,
            uint serverTick,
            bool isNewRequest,
            bool validAim)
        {
            if (_logRequests)
            {
                Debug.Log(
                    $"[网络射击/接收回执] Request={requestSequence}, " +
                    $"ServerTick={serverTick}, " +
                    $"NewRequest={isNewRequest}, ValidAim={validAim}, " +
                    "本阶段未执行射击",
                    this);
            }
        }

        #endregion

        #region 手动通信诊断

        [ContextMenu("诊断/发送一次射击请求（不生成子弹）")]
        private void SendDiagnosticRequest()
        {
            if (!Application.isPlaying ||
                !isActiveAndEnabled ||
                !IsClientInitialized ||
                !IsOwner)
            {
                Debug.LogWarning(
                    "[网络射击] 请在运行中的本地 Owner 玩家上发送请求",
                    this);
                return;
            }

            if (_player == null ||
                _player.RuntimeData == null ||
                _requestSource == null)
            {
                Debug.LogWarning(
                    "[网络射击] 缺少角色数据或请求采样组件",
                    this);
                return;
            }

            if (!TryGetConfirmedEquipmentRevision(
                _player.RuntimeData.CurrentItem,
                out uint serverEquipmentRevision))
            {
                Debug.LogWarning("[网络射击] 当前手持物尚未对应到服务器确认装备",this);
                return;
            }

            Camera localCamera = Camera.main;

            if (localCamera == null)
            {
                Debug.LogWarning("[网络射击] 找不到本地 MainCamera",this);
                return;
            }

            _requestSource.SetAimCamera(localCamera);

            if (!_requestSource.TryCreateRequest(
                    serverEquipmentRevision,
                    out WeaponFireRequest request))
            {
                Debug.LogWarning("[网络射击] 请求创建失败，请检查采样组件和相机",this);
                return;
            }

            TrySendFireRequest(request, TimeManager.LocalTick);
        }

        #endregion
    }
}
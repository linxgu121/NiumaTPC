using System.Collections.Generic;
using FishNet.Connection;
using FishNet.Object;
using NiumaTPC.Character;
using NiumaTPC.Character.Simulation;
using NiumaTPC.Item;
using UnityEngine;

namespace NiumaTPC.FishNet
{
    /// <summary>
    /// 武器请求的 FishNet 适配入口
    /// 服务器验证射击请求、登记逻辑子弹并返回处理结果
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NiumaCharacterController))]
    public sealed class WeaponDriver : NetworkBehaviour, INetworkWeaponFireRequestSender
    {
        #region Inspector

        [SerializeField]
        [Tooltip("绑定同根节点的 OfflineWeaponFireSource，本步只复用它采样请求；为空时获取同物体组件")]
        private OfflineWeaponFireSource _requestSource;

        [SerializeField]
        [Tooltip("打印请求提交、服务器接收与拥有者回执；接入连续开火后建议关闭")]
        private bool _logRequests = true;

        [Header("服务器请求时间窗")]

        [SerializeField, Min(0f)]
        [Tooltip("允许请求落后服务器的最大秒数，超过后拒绝，不补发过期子弹")]
        private float _maxRequestAge = 0.75f;

        [SerializeField, Min(0f)]
        [Tooltip("允许客户端估算时间领先服务器的最大秒数，用于容纳时钟估算误差")]
        private float _maxRequestFutureLead = 0.1f;

        [Header("服务器射击视点")]

        [SerializeField, Min(0.1f)]
        [Tooltip("准星射线起点距离服务器角色胶囊中心的最大距离，单位米")]
        private float _maxAimOriginDistance = 4f;

        [SerializeField, Min(0f)]
        [Tooltip("视点相对胶囊中心、垂直于瞄准方向的最大偏移，容纳肩位和高度差")]
        private float _maxAimOriginLateralDistance = 2f;

        [SerializeField, Min(0f)]
        [Tooltip("允许视点沿瞄准方向超出胶囊中心的距离，不是子弹向前偏移量")]
        private float _maxAimOriginForwardDistance = 0.5f;

        [SerializeField, Min(0.001f)]
        [Tooltip("视点遮挡探测球半径，单位米，不改变真实子弹的碰撞半径")]
        private float _aimProbeRadius = 0.05f;

        [SerializeField]
        [Tooltip("阻挡射击视点的实体层，包含墙体和地形，不得设为 Nothing；自动排除射手自身，忽略 Trigger")]
        private LayerMask _aimObstructionMask = Physics.DefaultRaycastLayers;

        #endregion

        #region 运行时状态

        private NiumaCharacterController _player;

        private NiumaFishNetPredictionDriver _predictionDriver;

        // 复用已有查询器及缓冲区，不另外创建弹道世界
        private readonly AmmunitionCollision _aimCollision = new AmmunitionCollision();

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

        // 客户端表现去重，与服务器请求消费编号分开，避免 Host 相互影响
        private ulong _lastPresentedFireSequence;

        // 限制在途请求数量，不是弹匣容量
        private const int MaxPendingOwnerShots = 64;

        // -1 表示尚未取得有效的服务器弹量
        private int _ownerAmmoInMagazine = -1;

        // 当前弹量快照已经覆盖到哪个射击请求
        private ulong _lastOwnerAmmoSequence;

        // 本客户端已经提交过的最大请求编号，防止重复预表现
        private ulong _lastOwnerSubmittedSequence;

        // 已预占弹量，但尚未被服务器快照覆盖的请求
        private readonly HashSet<ulong> _pendingOwnerShots = new();

        private struct OwnerPredictedShot
        {
            public ShotRequestKey Key;
            public AmmunitionPresenter Presenter;
        }

        // 这是视觉关联，不是弹量预占集合
        private readonly Dictionary<ulong, OwnerPredictedShot> _ownerPredictedShots = new();

        private bool HasOwnerFireBudget =>
            _ownerAmmoInMagazine > _pendingOwnerShots.Count &&
            _pendingOwnerShots.Count < MaxPendingOwnerShots;

        #endregion

        #region 生命周期

        private void Awake()
        {
            _player = GetComponent<NiumaCharacterController>();

            _predictionDriver = GetComponent<NiumaFishNetPredictionDriver>();

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
            ClearOwnerPredictions();
            _lastPresentedFireSequence = 0UL;
            _lastOwnerSubmittedSequence = 0UL;
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
            ClearOwnerPredictions();
            _lastPresentedFireSequence = 0UL;
            _lastOwnerSubmittedSequence = 0UL;
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
                instanceId, definitionId,
                FindServerEquipmentSlot(_serverEquippedItem),
                _serverEquippedItem?.WeaponState?.AmmoInMagazine ?? -1,
                _lastReceivedRequestSequence);
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
            int slotIndex,
            int ammoInMagazine,
            ulong processedRequestSequence)
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

            // 换了服务器装备代次，不能沿用上一把装备的弹量预测
            if (!_hasOwnerEquipmentSnapshot || equipmentRevision != _ownerEquipmentRevision)
            {
                ResetOwnerAmmoSnapshot();
            }

            _hasOwnerEquipmentSnapshot = true;
            _ownerEquipmentRevision = equipmentRevision;
            _ownerEquipmentInstanceId = instanceId ?? string.Empty;
            _ownerEquipmentSlotIndex = slotIndex;
            _ownerEquipmentDefinitionId = definitionId ?? string.Empty;

            ApplyOwnerAmmoSnapshot(
                equipmentRevision,
                ammoInMagazine,
                processedRequestSequence);

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
            ResetOwnerAmmoSnapshot();
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

            // 没有取得弹量，或者剩余弹量已被在途请求预占
            if (!HasOwnerFireBudget)
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

            uint estimatedServerTick = TimeManager.Tick;

            if (!TrySendFireRequest(request, estimatedServerTick))
            {
                return false;
            }

            // 只推进发送节流，不扣弹、不修改武器权威冷却
            _nextFireSubmissionTime = now + Mathf.Max(0.001f, weapon.FireRate);

            return true;
        }

        /// <summary>
        /// 本地预占弹量并播放反馈，再提交服务器
        /// true 仅表示已经提交，不代表服务器接受
        /// </summary>
        private bool TrySendFireRequest(
            WeaponFireRequest request,
            uint estimatedServerTick)
        {
            if (!isActiveAndEnabled ||
                !IsClientInitialized ||
                !IsOwner ||
                !HasOwnerFireBudget ||
                request.RequestSequence == 0UL ||
                request.RequestSequence <= _lastOwnerSubmittedSequence)
            {
                return false;
            }

            ItemInstance localItem = _player != null
                ? _player.RuntimeData?.CurrentItem
                : null;

            if (!TryGetConfirmedEquipmentRevision(
                    localItem,
                    out uint confirmedRevision) ||
                request.EquipmentRevision != confirmedRevision)
            {
                return false;
            }

            // 先记录，再调用 RPC，不能依赖 Host 的回调执行时机
            _lastOwnerSubmittedSequence = request.RequestSequence;
            _pendingOwnerShots.Add(request.RequestSequence);

            // 本发请求已经采样完成
            // 这里的后坐力只影响后续输入，不改变本发射线
            bool played = TryPlayOwnerFire(localItem);

            // 使用已经采样好的请求射线，不重新读取后坐力之后的相机
            TryBeginOwnerPrediction(localItem, request);

            if (_logRequests)
            {
                Debug.Log(
                    $"[网络射击/提交及预表现] ObjectId={ObjectId}, " +
                    $"Request={request.RequestSequence}, " +
                    $"EquipmentRevision={request.EquipmentRevision}, " +
                    $"PreviewPlayed={played}, " +
                    $"Pending={_pendingOwnerShots.Count}",
                    this);
            }

            SubmitFireServerRpc(request, estimatedServerTick);
            return true;
        }

        #endregion

        #region 服务器接收

        [ServerRpc(RequireOwnership = true, RunLocally = false)]
        private void SubmitFireServerRpc(
            WeaponFireRequest request,
            uint estimatedServerTick,
            NetworkConnection sender = null)
        {
            if (!IsServerInitialized ||
                sender == null ||
                !sender.IsActive ||
                sender != Owner)
            {
                return;
            }

            uint serverTick = TimeManager.Tick;
            double serverTime = Time.timeAsDouble;

            bool prechecksPassed = TryValidateServerRequestBasics(
                request,
                out WeaponFireRejectReason rejectReason);

            if (prechecksPassed)
            {
                prechecksPassed = TryValidateServerRequestTime(
                    estimatedServerTick,
                    serverTick,
                    out rejectReason);
            }

            if (prechecksPassed)
            {
                prechecksPassed = TryValidateServerCharacterState(out rejectReason);
            }

            if (prechecksPassed)
            {
                prechecksPassed = TryValidateServerWeaponState(
                    serverTime,
                    out rejectReason);
            }

            // 保存实际通过检查的射线，发射时不重新采样相机
            Ray approvedAim = default;

            if (prechecksPassed)
            {
                prechecksPassed = TryValidateServerAim(
                    request,
                    out approvedAim,
                    out rejectReason);
            }

            ulong shotId = 0UL;
            bool accepted = false;

            if (prechecksPassed)
            {
                accepted = TryExecuteServerShot(
                    approvedAim,
                    serverTime,
                    request.RequestSequence,
                    out shotId,
                    out rejectReason);
            }

            if (accepted)
            {
                // 逻辑子弹和扣弹已经提交，这里只广播角色武器的表现
                ObserveConfirmedFire(
                    request.RequestSequence,
                    _serverEquipmentRevision,
                    FindServerEquipmentSlot(_serverEquippedItem),
                    _serverEquippedItem.BaseData.ItemID);
            }

            // -1 表示当前没有有效的服务器武器状态
            int ammoInMagazine =
                _serverEquippedItem?.WeaponState?.AmmoInMagazine ?? -1;

            if (_logRequests)
            {
                Debug.Log(
                    $"[网络射击/服务器结果] Sender={sender.ClientId}, " +
                    $"ObjectId={ObjectId}, Request={request.RequestSequence}, " +
                    $"ServerTick={serverTick}, Accepted={accepted}, " +
                    $"ShotId={shotId}, Ammo={ammoInMagazine}, " +
                    $"Reason={rejectReason}",
                    this);
            }

            TargetReceiveFireResult(
                sender,
                request.RequestSequence,
                serverTick,
                accepted,
                shotId,
                ammoInMagazine,
                rejectReason,
                _serverEquipmentRevision,
                _lastReceivedRequestSequence);
        }

        /// <summary>
        /// 检查请求基础信息与服务器当前装备
        /// 新请求即使检查失败也会消费编号，防止之后重放
        /// </summary>
        private bool TryValidateServerRequestBasics(
            WeaponFireRequest request,
            out WeaponFireRejectReason rejectReason)
        {
            rejectReason = WeaponFireRejectReason.None;

            if (request.RequestSequence == 0UL)
            {
                rejectReason = WeaponFireRejectReason.InvalidRequestSequence;
                return false;
            }

            if (request.RequestSequence <= _lastReceivedRequestSequence)
            {
                rejectReason = WeaponFireRejectReason.StaleRequest;
                return false;
            }

            // 在后续检查前消费编号
            // 装备不匹配的请求不能等切枪后再拿来执行
            _lastReceivedRequestSequence = request.RequestSequence;

            if (!isActiveAndEnabled || _serverEquipmentRevision == 0u)
            {
                rejectReason = WeaponFireRejectReason.Unavailable;
                return false;
            }

            // 只检查数值合法性，不代表已认可视点位置和遮挡关系
            if (!request.TryGetAimRay(out _))
            {
                rejectReason = WeaponFireRejectReason.InvalidAim;
                return false;
            }

            // 只读取服务器保存的装备记录
            // 不使用 Owner 快照或本地 RuntimeData.CurrentItem 代替
            if (request.EquipmentRevision == 0u ||
                request.EquipmentRevision != _serverEquipmentRevision ||
                _serverEquippedItem == null ||
                _serverEquippedItem.CurrentAmount <= 0 ||
                FindServerEquipmentSlot(_serverEquippedItem) < 0)
            {
                rejectReason = WeaponFireRejectReason.EquipmentMismatch;
                return false;
            }

            if (!(_serverEquippedItem.BaseData is RangedWeaponSO) ||
                _serverEquippedItem.WeaponState == null)
            {
                rejectReason = WeaponFireRejectReason.InvalidWeapon;
                return false;
            }

            return true;
        }

        /// <summary>
        /// 检查服务器模拟中的动作与瞄准状态
        /// 不读取本地设备输入开关或动画表现状态
        /// </summary>
        private bool TryValidateServerCharacterState(
            out WeaponFireRejectReason rejectReason)
        {
            rejectReason = WeaponFireRejectReason.None;

            if (_predictionDriver == null ||
                !_predictionDriver.TryGetServerSimulationState(
                    out CharacterSimulationState state))
            {
                rejectReason = WeaponFireRejectReason.Unavailable;
                return false;
            }

            // 当前规则：翻越、翻滚、闪避、滑铲期间禁止射击
            // 不额外限制必须落地，避免误伤空中瞄准射击
            if (state.VaultType != VaultType.None ||
                state.ActionType != CharacterActionType.None)
            {
                rejectReason = WeaponFireRejectReason.ActionBlocked;
                return false;
            }

            // 必须是服务器已经模拟到的瞄准状态
            if (!state.IsAiming)
            {
                rejectReason = WeaponFireRejectReason.NotAiming;
                return false;
            }

            return true;
        }

        /// <summary>
        /// 检查服务器武器实例的弹量与冷却
        /// 这里只检查，不扣弹、不推进冷却
        /// </summary>
        private bool TryValidateServerWeaponState(
            double serverTime,
            out WeaponFireRejectReason rejectReason)
        {
            rejectReason = WeaponFireRejectReason.None;

            // 使用服务器保存的实例，不读取客户端表现物品
            RangedWeaponRuntimeState state =
                _serverEquippedItem?.WeaponState;

            if (state == null)
            {
                rejectReason = WeaponFireRejectReason.InvalidWeapon;
                return false;
            }

            // 复用已有状态判断，只在这里区分拒绝原因
            if (!state.CanFire(serverTime))
            {
                rejectReason = state.AmmoInMagazine <= 0
                    ? WeaponFireRejectReason.EmptyMagazine
                    : WeaponFireRejectReason.Cooldown;

                return false;
            }

            return true;
        }

        /// <summary>
        /// 登记服务器逻辑子弹，并在成功后提交弹量与冷却
        /// 必须在所有请求检查通过后调用
        /// </summary>
        private bool TryExecuteServerShot(
            Ray approvedAim,
            double serverTime,
            ulong requestSequence,
            out ulong shotId,
            out WeaponFireRejectReason rejectReason)
        {
            shotId = 0UL;
            rejectReason = WeaponFireRejectReason.None;

            ItemInstance item = _serverEquippedItem;

            if (item == null ||
                !(item.BaseData is RangedWeaponSO weapon) ||
                item.WeaponState == null)
            {
                rejectReason = WeaponFireRejectReason.InvalidWeapon;
                return false;
            }

            RangedWeaponRuntimeState state = item.WeaponState;

            // 提交前确认同一实例仍然允许射击
            if (!state.CanFire(serverTime))
            {
                rejectReason = state.AmmoInMagazine <= 0
                    ? WeaponFireRejectReason.EmptyMagazine
                    : WeaponFireRejectReason.Cooldown;

                return false;
            }

            if (!AmmunitionDriver.TryGetForScene(
                    gameObject.scene,
                    out AmmunitionDriver driver) ||
                driver.NetworkManager != NetworkManager)
            {
                rejectReason = WeaponFireRejectReason.Unavailable;
                return false;
            }

            double fireInterval = Mathf.Max(0.001f, weapon.FireRate);

            // 来源身份由服务器实际持有的装备构造
            // 不允许客户端自行指定服务器武器实例
            ShotRequestKey requestKey = new(
                item.InstanceID,
                _serverEquipmentRevision,
                requestSequence);

            if (!driver.TrySpawn(
                    weapon,
                    approvedAim,
                    transform,
                    requestKey,
                    out shotId))
            {
                rejectReason = WeaponFireRejectReason.SpawnFailed;
                return false;
            }

            // 登记和提交之间不执行外部回调，也不等待下一帧
            // 同一次流程使用同一个服务器时间样本
            state.CommitAcceptedShot(serverTime, fireInterval);

            // 扣弹已经提交，之后才允许发布表现消息
            driver.PublishAcceptedShot(shotId);

            return true;
        }

        #endregion

        #region 请求时序与视点检查

        private bool TryValidateServerRequestTime(
            uint estimatedServerTick,
            uint serverTick,
            out WeaponFireRejectReason rejectReason)
        {
            rejectReason = WeaponFireRejectReason.None;

            // 使用有符号环差，避免 uint 相减下溢
            // 正数表示请求落后，负数表示请求超前
            int tickDifference = unchecked(
                (int)(serverTick - estimatedServerTick));

            double ageSeconds = tickDifference * TimeManager.TickDelta;

            if (ageSeconds > _maxRequestAge)
            {
                rejectReason = WeaponFireRejectReason.RequestExpired;
                return false;
            }

            if (ageSeconds < -_maxRequestFutureLead)
            {
                rejectReason = WeaponFireRejectReason.RequestFromFuture;
                return false;
            }

            return true;
        }

        /// <summary>
        /// 检查准星射线的视点范围、俯仰范围与遮挡
        /// 不读取模型枪口，不修改请求的射击方向
        /// </summary>
        private bool TryValidateServerAim(
            WeaponFireRequest request,
            out Ray aimRay,
            out WeaponFireRejectReason rejectReason)
        {
            rejectReason = WeaponFireRejectReason.None;

            if (!request.TryGetAimRay(out aimRay))
            {
                rejectReason = WeaponFireRejectReason.InvalidAim;
                return false;
            }

            if (_player == null ||
                _player.CharacterController == null ||
                _player.Config == null ||
                _player.Config.Core == null ||
                _predictionDriver == null ||
                !_predictionDriver.TryGetServerSimulationState(
                    out CharacterSimulationState state))
            {
                rejectReason = WeaponFireRejectReason.Unavailable;
                return false;
            }

            CharacterController controller = _player.CharacterController;

            // 使用服务器模拟位置和胶囊中心
            // 不使用平滑模型、头部骨骼或客户端相机作为权威锚点
            Vector3 centerOffset = Vector3.Scale(
                controller.center,
                controller.transform.lossyScale);

            Vector3 anchor = state.Position + Quaternion.Euler(0f, state.Yaw, 0f) * centerOffset;

            Vector3 originOffset = aimRay.origin - anchor;

            if (originOffset.sqrMagnitude > _maxAimOriginDistance * _maxAimOriginDistance)
            {
                rejectReason = WeaponFireRejectReason.InvalidAimOrigin;
                return false;
            }

            // 将偏移拆成沿瞄准方向与垂直于瞄准方向的两部分
            float forwardOffset = Vector3.Dot(
                originOffset,
                aimRay.direction);

            Vector3 lateralOffset =
                originOffset - aimRay.direction * forwardOffset;

            if (forwardOffset > _maxAimOriginForwardDistance ||
                lateralOffset.sqrMagnitude >
                _maxAimOriginLateralDistance * _maxAimOriginLateralDistance)
            {
                rejectReason = WeaponFireRejectReason.InvalidAimOrigin;
                return false;
            }

            // Unity 绕 X 轴的正角度对应向下看
            float pitch = -Mathf.Asin(
                Mathf.Clamp(aimRay.direction.y, -1f, 1f)) * Mathf.Rad2Deg;

            Vector2 pitchLimits = _player.Config.Core.PitchLimits;

            const float pitchTolerance = 2f;

            float minimumPitch =
                Mathf.Min(pitchLimits.x, pitchLimits.y) - pitchTolerance;

            float maximumPitch =
                Mathf.Max(pitchLimits.x, pitchLimits.y) + pitchTolerance;

            if (pitch < minimumPitch || pitch > maximumPitch)
            {
                rejectReason = WeaponFireRejectReason.InvalidAim;
                return false;
            }

            PhysicsScene physicsScene = gameObject.scene.GetPhysicsScene();

            if (!physicsScene.IsValid() || _aimObstructionMask.value == 0)
            {
                rejectReason = WeaponFireRejectReason.Unavailable;
                return false;
            }

            // 同步已有 Transform 更改，不推进物理模拟
            Physics.SyncTransforms();

            // 这里只借用状态结构承载查询参数
            // 没有登记到 AmmunitionWorld，不会生成子弹或占用 ShotId
            AmmunitionState probe = new AmmunitionState(
                0UL,
                anchor,
                Vector3.zero,
                Vector3.zero,
                _aimProbeRadius,
                1f);

            // 先检查锚点，再检查通往请求视点的路径
            // 查询结果溢出也不能视为无遮挡
            if (_aimCollision.CheckInitialOverlap(
                    physicsScene,
                    in probe,
                    transform,
                    _aimObstructionMask,
                    QueryTriggerInteraction.Ignore,
                    out _) != AmmunitionCastResult.Clear ||
                _aimCollision.Cast(
                    physicsScene,
                    in probe,
                    aimRay.origin,
                    transform,
                    _aimObstructionMask,
                    QueryTriggerInteraction.Ignore,
                    out _) != AmmunitionCastResult.Clear)
            {
                rejectReason = WeaponFireRejectReason.AimOriginBlocked;
                return false;
            }

            // 终点也检查一次，避免视点本身嵌在阻挡物中
            probe.Position = aimRay.origin;

            if (_aimCollision.CheckInitialOverlap(
                    physicsScene,
                    in probe,
                    transform,
                    _aimObstructionMask,
                    QueryTriggerInteraction.Ignore,
                    out _) != AmmunitionCastResult.Clear)
            {
                rejectReason = WeaponFireRejectReason.AimOriginBlocked;
                return false;
            }

            return true;
        }

        #endregion

        #region 已确认开火表现广播

        [ObserversRpc(RunLocally = false, BufferLast = false, ExcludeOwner = true, ExcludeServer = false)]
        private void ObserveConfirmedFire(
            ulong requestSequence,
            uint equipmentRevision,
            int slotIndex,
            string weaponDefinitionId)
        {
            if (!IsClientInitialized ||
                IsOwner ||
                requestSequence == 0UL ||
                requestSequence <= _lastPresentedFireSequence)
            {
                return;
            }

            // 未就绪的模型也消费本次事件，不在以后补播旧枪声
            _lastPresentedFireSequence = requestSequence;

            if (_player == null ||
                _player.RuntimeData == null ||
                _player.InventoryController == null ||
                _player.EquipmentDriver == null ||
                equipmentRevision == 0u ||
                slotIndex < 0)
            {
                return;
            }

            var hotbar = _player.InventoryController.HotbarInventory;
            if (hotbar == null)
            {
                return;
            }

            ItemInstance localItem = hotbar.GetAt(slotIndex);

            // 当前仅支持固定出生槽位映射，不比较各端独立生成的实例 ID
            if (localItem == null ||
                localItem.BaseData == null ||
                localItem.BaseData.ItemID != weaponDefinitionId ||
                !ReferenceEquals(_player.RuntimeData.CurrentItem, localItem))
            {
                return;
            }


            var equipment = _player.EquipmentDriver;
            if (!ReferenceEquals(equipment.CurrentItemInstance, localItem) ||
                !(equipment.CurrentItemDirector is MonoBehaviour component) ||
                component == null ||
                !component.isActiveAndEnabled ||
                !(equipment.CurrentItemDirector is IWeaponFirePresentation presentation))
            {
                return;
            }

            // 此处只有其他玩家的确认表现，不影响本地摄像机
            bool played = presentation.TryPlayFire(localItem, applyRecoil: false);

            if (_logRequests)
            {
                Debug.Log(
                    $"[网络射击/开火表现] ObjectId={ObjectId}, " +
                    $"Request={requestSequence}, Played={played}, " +
                    $"LocalRecoil={IsOwner}",
                    this);
            }
        }

        #endregion

        #region Owner 弹量预测与即时表现

        private void ResetOwnerAmmoSnapshot()
        {
            _ownerAmmoInMagazine = -1;
            _lastOwnerAmmoSequence = 0UL;
            _pendingOwnerShots.Clear();
        }

        private void ApplyOwnerAmmoSnapshot(
            uint equipmentRevision,
            int ammoInMagazine,
            ulong processedRequestSequence)
        {
            if (!IsClientInitialized ||
                !IsOwner ||
                !_hasOwnerEquipmentSnapshot ||
                equipmentRevision != _ownerEquipmentRevision ||
                processedRequestSequence < _lastOwnerAmmoSequence)
            {
                return;
            }

            _ownerAmmoInMagazine = Mathf.Max(-1, ammoInMagazine);
            _lastOwnerAmmoSequence = processedRequestSequence;

            // 这份弹量已经包含这些请求的处理结果
            // 不论接受还是拒绝，都不能继续重复预占
            _pendingOwnerShots.RemoveWhere(
                sequence => sequence <= processedRequestSequence);
        }

        private bool TryPlayOwnerFire(ItemInstance expectedItem)
        {
            var equipment = _player != null
                ? _player.EquipmentDriver
                : null;

            if (!IsOwner ||
                expectedItem == null ||
                equipment == null ||
                !ReferenceEquals(
                    equipment.CurrentItemInstance,
                    expectedItem) ||
                !(equipment.CurrentItemDirector is MonoBehaviour component) ||
                component == null ||
                !component.isActiveAndEnabled ||
                !(equipment.CurrentItemDirector is IWeaponFirePresentation presentation))
            {
                return false;
            }

            // 本地只播放反馈，不生成子弹或提交弹药消耗
            return presentation.TryPlayFire(
                expectedItem,
                applyRecoil: true);
        }

        #endregion

        #region Owner 预测子弹

        private void TryBeginOwnerPrediction(
            ItemInstance localItem,
            WeaponFireRequest request)
        {
            if (!IsOwner ||
                localItem == null ||
                !(localItem.BaseData is RangedWeaponSO weapon) ||
                _ownerPredictedShots.Count >= MaxPendingOwnerShots ||
                _ownerPredictedShots.ContainsKey(request.RequestSequence) ||
                !request.TryGetAimRay(out Ray aim))
            {
                return;
            }

            if (!AmmunitionPresenter.TryGetForScene(
                    gameObject.scene,
                    out AmmunitionPresenter presenter))
            {
                return;
            }

            var worldDriver =
                presenter.GetComponent<AmmunitionDriver>();

            if (worldDriver == null ||
                worldDriver.NetworkManager != NetworkManager)
            {
                return;
            }

            // 必须使用服务器同步的实例 ID
            ShotRequestKey key = new(
                _ownerEquipmentInstanceId,
                request.EquipmentRevision,
                request.RequestSequence);

            if (!presenter.TryPlayPrediction(key, weapon, aim))
            {
                return;
            }

            _ownerPredictedShots.Add(
                request.RequestSequence,
                new OwnerPredictedShot
                {
                    Key = key,
                    Presenter = presenter
                });
        }

        private void ResolveOwnerPrediction(
            ulong requestSequence,
            bool accepted)
        {
            if (!_ownerPredictedShots.TryGetValue(
                    requestSequence,
                    out OwnerPredictedShot prediction))
            {
                return;
            }

            _ownerPredictedShots.Remove(requestSequence);

            if (!accepted && prediction.Presenter != null)
            {
                prediction.Presenter.CancelPrediction(prediction.Key);
            }

            // 接受时不再创建或认领子弹
            // 等待弹道世界携带完整轨迹的发射广播完成认领
        }

        private void ClearOwnerPredictions()
        {
            foreach (OwnerPredictedShot prediction in _ownerPredictedShots.Values)
            {
                if (prediction.Presenter != null)
                {
                    prediction.Presenter.CancelPrediction(prediction.Key);
                }
            }

            _ownerPredictedShots.Clear();
        }

        #endregion

        #region 拥有者回执

        [TargetRpc(RunLocally = false)]
        private void TargetReceiveFireResult(
            NetworkConnection target,
            ulong requestSequence,
            uint serverTick,
            bool accepted,
            ulong shotId,
            int ammoInMagazine,
            WeaponFireRejectReason rejectReason,
            uint equipmentRevision,
            ulong processedRequestSequence)
        {
            if (!IsClientInitialized || !IsOwner)
            {
                return;
            }

            ResolveOwnerPrediction(requestSequence, accepted);

            // 接受与拒绝都使用服务器实际弹量修正本地预算
            // 回执不再次播放枪声、枪焰或后坐力
            ApplyOwnerAmmoSnapshot(
                equipmentRevision,
                ammoInMagazine,
                processedRequestSequence);

            if (_logRequests)
            {
                Debug.Log(
                    $"[网络射击/结果回执] Request={requestSequence}, " +
                    $"ServerTick={serverTick}, Accepted={accepted}, " +
                    $"ShotId={shotId}, Ammo={ammoInMagazine}, " +
                    $"Reason={rejectReason}",
                    this);
            }
        }

        #endregion

        #region 手动通信诊断

        [ContextMenu("诊断/提交一次真实射击请求（成功会扣弹）")]
        private void SendDiagnosticRequest()
        {
            if (!Application.isPlaying ||
                !isActiveAndEnabled ||
                !IsClientInitialized ||
                !IsOwner)
            {
                Debug.LogWarning("[网络射击] 请在运行中的本地 Owner 玩家上发送请求", this);
                return;
            }

            if (_player == null ||
                _player.RuntimeData == null ||
                _requestSource == null)
            {
                Debug.LogWarning("[网络射击] 缺少角色数据或请求采样组件", this);
                return;
            }

            if (!TryGetConfirmedEquipmentRevision(
                _player.RuntimeData.CurrentItem,
                out uint serverEquipmentRevision))
            {
                Debug.LogWarning("[网络射击] 当前手持物尚未对应到服务器确认装备", this);
                return;
            }

            Camera localCamera = Camera.main;

            if (localCamera == null)
            {
                Debug.LogWarning("[网络射击] 找不到本地 MainCamera", this);
                return;
            }

            _requestSource.SetAimCamera(localCamera);

            if (!_requestSource.TryCreateRequest(
                    serverEquipmentRevision,
                    out WeaponFireRequest request))
            {
                Debug.LogWarning("[网络射击] 请求创建失败，请检查采样组件和相机", this);
                return;
            }

            TrySendFireRequest(request, TimeManager.Tick);
        }

        #endregion
    }
}

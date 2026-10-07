# NiumaTPC

## Network-Ready Action Controller Framework

**NiumaTPC** 是一个意图驱动、管线化的 Unity 第三人称动作控制器框架。它以“数据中心黑板 + 分层状态机 + 仲裁器 + 固定 Tick 模拟”组织移动、动作、装备和表现系统，并通过外观模式（Facade）与驱动器（Driver）解耦动画、IK、音频、相机和网络后端。

框架面向需要持续扩展的第三人称与动作类项目。当输入、状态切换、动画事件、装备、IK、音频和网络预测逐渐纠缠在一起时，NiumaTPC 将它们拆成职责明确、可以独立替换的层级，并让离线模式与联网模式复用同一套角色模拟规则。

## 本次更新：脚本弹道与网络射击接入（2026-10-07）

### 新增与调整

- **脚本驱动弹道：** 新增 `AmmunitionState`、轨迹计算、碰撞查询、模拟器与 `AmmunitionWorld`，记录位置、速度、寿命、累计飞行距离和结束状态。支持射线/球形扫掠、初始重叠检测及曲线子步，不再由枪械给 Rigidbody 施加速度来驱动这条发射流程。
- **通用枪械与弹药配置：** 将 `AKSO` 更名为 `GunWeaponSO` 并保留脚本 GUID；`RangedWeaponSO` 配置初速度、开火间隔与弹药引用，`AmmunitionDefinitionSO` 配置重力、碰撞半径、寿命和表现资源。`FireRate` 的单位仍是两次开火间隔的秒数。
- **子弹及命中表现：** 新增 `AmmunitionView`、`AmmunitionImpactView`，通过对象池管理视觉子弹、拖尾、命中特效和弹坑，支持命中、寿命结束及组件停用时清理回收。命中音效单次播放，特效自身的 AudioSource 配置不由命中特效脚本覆盖。
- **离线射击闭环：** 新增 `OfflineWeaponFireSource` 与 `OfflineAmmunitionDriver`，从当前玩家相机中心采样射线，登记逻辑子弹并统一推进；枪口只负责声光和后坐力表现。角色生成器在激活前注入射击相机与弹道驱动。
- **武器独立运行时状态：** `ItemInstance` 持有弹匣与冷却状态，切换或回收武器模型不会重新补满弹药。`PlayerRuntimeData` 增加装备代次；请求包含序号、装备代次和瞄准快照，并提供拒绝原因及非法浮点数检查。
- **FishNet 射击请求：** 新增 `INetworkWeaponFireRequestSender` 和 `NiumaFishNetWeaponDriver`，接入 Owner 提交、发送节流、ServerRpc 接收及 TargetRpc 回执。网络角色发送失败或断线时不会回退到离线发射，Runtime 不反向依赖 FishNet。
- **服务器装备记录：** 服务器根据角色出生配置创建独立装备实例，向 Owner 同步实例 ID、配置 ID、槽位和装备代次；本地通过出生槽位对应表现实例，网络开火请求使用服务器确认的代次。
- **初始化保护：** 角色基础属性、运行数据或运动驱动未准备好时，不再启动后续子系统或执行出池复位；补齐模拟配置工厂的文档注释。

### 使用与配置

1. **枪械与弹药：** 在 `GunWeaponSO` 上设置继承的 `ProjectileSpeed`、`FireRate`、`MaxAmmo` 与 `Ammunition`。弹药 `CollisionRadius=0` 使用射线，大于 0 使用球形扫掠；`GravityScale=0` 不受重力影响；`MaxLifetime` 控制逻辑子弹最大存活时间。
2. **离线场景：** 放置一个 `OfflineAmmunitionDriver` 管理该物理世界的多种武器子弹，不要每把枪挂一个。`hitMask` 默认使用物理默认检测层，需包含墙体与目标；`triggerInteraction` 默认忽略 Trigger，使用触发器受击框时改为 Collide。配置视觉资源时必须绑定场景中的 `SimpleObjectPoolSystem` 到 `visualPool`；`maxActiveImpacts` 默认 128，设为 0 关闭命中特效，超限回收最早的效果。
3. **离线角色：** 根节点挂 `OfflineWeaponFireSource`；在 `OfflineCharacterSpawner` 上绑定 `shootingCamera` 与 `ammunitionDriver`，由生成器注入。手动放置角色时直接绑定采样器的相机和驱动。相机必须是实际渲染玩家画面的启用透视 Camera。
4. **表现预制体：** 弹药 `VisualPrefab` 根节点挂 `AmmunitionView`，其 TrailRenderer 可留空；`ImpactPrefab` 根节点挂 `AmmunitionImpactView`，`particleRoot` 留空可显示静态弹坑，`retainTime` 默认 5 秒，`surfaceOffset` 默认 0。未配置相应表现资源时不生成该表现；`ImpactSound` 为空时不播放命中音效。
5. **网络角色：** 在角色根 NetworkObject 同物体挂 `NiumaFishNetWeaponDriver` 和 `OfflineWeaponFireSource`。`_requestSource` 绑定同根采样器，留空时尝试同物体查找；Owner 提交时自动注入 `Camera.main`，此阶段不需要给网络采样器绑定离线弹道驱动。`_logRequests` 默认开启，持续开火排查结束后可关闭；出生装备复用角色的 `DefaultEquipment1/2/3`，全部留空时服务器确认空手。

### 当前阶段边界

- **网络射击尚未完成服务器实弹执行。** 当前 ServerRpc 检查连接所有权、请求序号与瞄准数值并返回接收回执；服务器装备代次授权、角色动作限制、合法视点/时序验证、弹量冷却提交、逻辑子弹发射及网络表现广播仍待接入。收到回执不表示已经允许开火或造成伤害。
- 网络路径暂不播放离线枪焰、音效、后坐力或生成离线子弹，避免把本地表现误当成服务器接受结果。完整装备切换授权、背包换槽和动态库存映射尚未接入，目前按固定出生槽位对应。
- `AKSO` 到 `GunWeaponSO` 保留脚本 GUID，但旧刚体子弹资源仍需按上面的新弹药配置和视图组件接线；本仓库提交的是模块源码及文档，不包含外部项目的场景、武器资产或 Prefab 改动。
- 本次提交未重新运行 Unity 编译或自动化测试，不将代码保存或提交视为网络阶段验收通过。

---

## 功能

### 第三人称移动

- 待机、行走、慢跑与冲刺。
- 八方向起步、循环移动、停止与快速反向处理。
- 自由视角和瞄准移动模式。
- 基于 `CharacterController` 的接地、碰撞、重力和空中控制。

### 身体动作

- 普通跳跃、按移动状态区分的起跳速度、二段跳、下落和多级落地。
- 翻滚与闪避，支持独立配置位移、持续时间、进度曲线和重力规则。
- 低位与高位翻越，支持环境探测、目标对齐、固定 Tick 轨迹和玩法开关。
- 滑铲输入、输入缓冲和配置结构；滑铲权威运动与表现状态仍在开发。

### 角色表现

- 全身、上半身和覆盖动作分层播放。
- 动画淡入淡出、速度控制、归一化时间、左右脚相和结束回调。
- 瞄准、手部 IK、翻越 IK 与不同 IK 后端适配。
- 相机跟随、自由观察、瞄准视角以及本地网络角色自动绑定。
- 装备、背包、武器行为、音效请求和对象池基础能力。

### 网络同步

- 本地拥有者输入采集与客户端预测。
- 服务器权威模拟和输入所有权校验。
- FishNet `Replicate/Reconcile` 回滚校正。
- 远端角色状态快照和动画表现同步。
- 离线固定 Tick 与 FishNet Tick 共用角色模拟核心。
- 摄像机跟随平滑后的图形节点，避免直接跟随固定 Tick 模拟根节点产生抖动。

---

## 设计理念

NiumaTPC 的核心原则是把“想做什么”“实际发生了什么”和“最终怎样表现”拆开。

- **意图与状态分离：** 输入只表达玩家或 AI 的瞬时意图，不直接修改角色状态。
- **模拟与表现分离：** 固定 Tick 模拟决定权威位置、速度和动作结果，状态机与动画只消费结果并负责表现。
- **配置与运行时分离：** `ScriptableObject` 负责策划配置，启动时转换为模拟层纯数据，Tick 中不直接读取资源对象。
- **离线与联网统一：** 离线驱动器和 FishNet 驱动器提交相同的输入命令，并调用相同的模拟器。
- **第三方后端可替换：** 动画、IK、相机和网络通过 Facade、Driver 或适配层接入，不反向污染角色核心。

这种结构让新增动作、替换输入源、切换动画或 IK 后端、接入 AI 和网络框架时，不必重新改写整套角色控制逻辑。

---

## 核心架构

### 1. 双轨信息流与输入管线

框架将角色数据分为两类：

- **意图（Intent）：** 这一刻想执行的行为，例如移动、跳跃、瞄准、翻滚、闪避、翻越或滑铲。
- **参数（Parameter）：** 驱动状态与表现的连续数据，例如速度、方向、视角、落地高度和动画权重。

`InputSourceBase` 允许玩家、AI 或其他系统提供输入；`InputPipeline` 负责采样、边沿检测、缓冲和消费；`MainProcessorPipeline` 再把数据交给意图处理器与参数处理器。每个处理器只处理一种职责，可以独立增加、替换或提前终止后续流程。

```text
Player / AI Input
        ↓
InputSource → InputPipeline
        ↓
Intent Processors + Parameter Processors
        ↓
CharacterInputCommand
```

### 2. 数据中心黑板

`PlayerRuntimeData` 与 `InputData` 是角色各子系统共享的数据中心。输入、状态机、仲裁器、动画和驱动器通过黑板交换结果，而不是彼此直接查找组件或建立大量双向依赖。

瞬时输入会在规定阶段消费或复位，持续状态则保存在明确的运行时字段中。这使“本帧按下”“当前持有”“已经执行”拥有不同的数据语义，也为输入量化、状态快照和网络回滚提供稳定边界。

### 3. 固定 Tick 可回滚模拟

`CharacterSimulationRunner` 是角色权威运动的统一入口。水平移动、重力、跳跃、动作位移和翻越轨迹都按固定 Tick 推进，不依赖渲染帧率。

模拟层由输入命令、纯数据配置、模拟状态和 `ICharacterSimulationBody` 组成，不直接依赖动画状态机。`CharacterControllerSimulationBody` 负责把模拟结果应用到 Unity `CharacterController`，并将碰撞后的真实位置反馈给模拟状态。

```text
CharacterInputCommand
        ↓
CharacterSimulationRunner
        ↓
Movement / Vertical / Action / Vault Simulator
        ↓
ICharacterSimulationBody
        ↓
CharacterSimulationState
```

模拟状态可以保存、恢复和重放，是客户端预测、服务器复算与误差校正的共同基础。

### 4. 分层状态机与统一打断

角色表现使用分层状态体系，而不是让所有状态互相硬编码切换：

- **全身层（FullBody）：** 管理待机、移动、跳跃、下落、落地、翻滚、闪避和翻越。
- **上半身层（UpperBody）：** 管理空手、持有物品、瞄准和装备表现。
- **覆盖动作（Override）：** 处理死亡、受击或其他高优先级强制动作。

状态注册表负责实例复用，`GlobalInterruptProcessor` 与 `UpperBodyInterruptProcessor` 统一执行打断检查。跳跃、下落、落地、瞄准、翻滚、闪避和翻越等进入条件由拦截器 SO 描述，避免把互斥规则散落在每个状态脚本中。

#### 持枪动画遮罩与 IK 配置

- `PlayerSO.Core.UpperBodyMask` 决定第 1 层装备动画覆盖哪些部位。当前示例使用 `Assets/Game/Player/Config/HeldItemArms.mask`：只启用双臂、手指和手部 IK，关闭 Body、Head、Root、双腿和足部 IK；Transform 列表为空，使用 Humanoid 部位映射，不绑定某个模型的骨骼路径。
- 躯干继续播放第 0 层的走跑动作，保留迈步时的躯干反向调整。将 Body 纳入静态持枪层可能覆盖这部分动作，导致胸口和枪跟着骨盆一起左右侧倾。正常的跑步转体仍会保留，这个配置并不是锁死上半身。
- 当前 `Player` 预制体的 `AimIK / Solver / Bones` 中，骨盆（`腰`）权重为 0，脊柱仍参与瞄准，避免枪口校正直接扭动下半身。`FullBodyBipedIK` 的骨盆引用仍需保留，不能因此删除。
- `HoldPositionOffset` / `HoldRotationOffset` 调整枪相对右手挂点的位置与朝向；武器上的 `LeftHandGoal` 决定左手握点。这些值与角色骨架、枪模型有关，不能直接复制另一角色的数值。先验证遮罩，再单独调握点，避免用 IK 强拉去掩盖动画层配置问题。

### 5. 仲裁器管线

`ArbiterPipeline` 为动作、生命、耐力和 LOD 请求提供统一决策入口。仲裁器处理冲突、优先级、资源条件与覆盖请求，再把被接受的结果写回黑板或提交给状态系统。

这使外部系统只需表达“请求执行某动作”，不必知道当前状态机内部结构，也不能绕过角色规则直接切换状态。

### 6. 表现层解耦

玩法逻辑不直接依赖具体动画机、IK 插件或音频实现。

- **Animation Facade：** `AnimationFacadeBase` 定义统一动画入口，`AnimancerFacade` 负责 Animancer 的分层播放、Mask、权重、回调和事件。
- **Motion Driver：** 处理输入驱动、烘焙曲线驱动和 Motion Warping，并把动画时间与离线运动数据对齐。
- **Equipment Driver：** 管理物品实例生成、挂点绑定和装备切换。
- **Audio Driver：** 集中处理角色音效请求，支持统一降级和替换音频后端。
- **IK Source：** `PlayerIKSourceBase` 抽象 IK 数据入口，可接入 Final IK 或 Unity Animation Rigging。

网络模式下，权威位移由固定 Tick 模拟产生；动画、IK 和音频只根据模拟结果进行表现，不能反向决定服务器位置。

### 7. FishNet 预测与服务器权威

FishNet 适配层位于独立程序集，不让核心角色模拟反向依赖网络框架。

本地拥有者采集输入并生成量化命令，先在客户端预测执行，再通过 FishNet 发送给服务器。服务器使用相同配置和模拟器重新计算结果，并通过 Reconcile 返回权威状态；客户端发生误差时恢复状态并重放后续输入。

```text
Local Owner Input
        ↓
Replicate Command
        ↓
Client Prediction ─────→ Immediate Local Presentation
        ↓
Server Authoritative Simulation
        ↓
Reconcile State
        ↓
Restore + Replay Pending Inputs
```

观察者角色消费同步后的表现状态，不读取本地输入。摄像机只绑定本地拥有者，并优先跟随 `NetworkTickSmoother` 的图形节点，使镜头更新与网络模拟根节点解耦。

---

## 子系统细节

- **输入系统：** `IInputSource`、`InputSourceBase` 与 `PlayerInputSource` 抽象输入来源；`InputPipeline` 负责输入采样、缓冲、消费和一致性处理。
- **处理管线：** `MainProcessorPipeline` 将逻辑拆为 Intent Processors 与 Parameter Processors，支持模块化扩展和 early-out。
- **运行时黑板：** `PlayerRuntimeData`、`InputData` 统一保存帧级意图、状态结果、装备引用和表现参数。
- **模拟配置：** `CharacterSimulationConfigFactory` 将 `PlayerSO` 配置转换为模拟层纯数据及预采样运动 Profile。
- **运动模拟：** 水平移动、垂直运动、动作位移、起步方向和翻越轨迹由独立 Simulator 处理。
- **环境探测：** 翻越等动作通过独立探测契约查询环境，模拟器只消费探测结果，不直接绑定具体射线实现。
- **状态系统：** 全身、上半身与覆盖动作分层组织，通过注册表复用状态实例。
- **打断系统：** 拦截器 SO 集中定义状态进入、打断和互斥规则。
- **仲裁系统：** Action、Health、Stamina 与 LOD 仲裁器集中处理冲突请求和优先级。
- **动画系统：** Animancer Facade 负责多层动画、权重、淡入淡出、相位同步、回调与远端表现。
- **运动烘焙：** Root Motion Baker 和 Warped Motion Baker 提取速度、旋转、脚相与空间特征点。
- **相机系统：** `CameraRigDriver`、`PlayerCameraManager` 与 FishNet Camera Binder 管理视角和本地拥有者绑定。
- **IK 系统：** `IKController` 统一读取 IK 数据，支持 Final IK 与 Unity Animation Rigging 适配。
- **装备与物品：** 包含物品定义、物品实例、背包堆叠、角色库存、装备驱动和武器行为示例。
- **音频系统：** `AudioSO`、`AudioDriver` 与 `AudioController` 以请求方式集中播放角色音效。
- **对象池：** `SimpleObjectPoolSystem` 为物品、特效和投射物提供通用复用能力。
- **编辑器工具：** 提供动画速度分析、Root Motion 提取、Warped Motion 提取和调试辅助工具。

---

## 性能设计

- **单入口驱动：** 角色帧更新和固定 Tick 模拟由少数明确入口统一调度，减少散落 `Update` 带来的调度与排查成本。
- **纯数据模拟：** ScriptableObject 在启动时转换为只读运行配置，模拟 Tick 不执行反射扫描，也不重复查询资源配置。
- **预采样运动曲线：** 起步、翻滚、闪避和翻越所需曲线提前转换为固定 Tick Profile，降低运行时求值差异并提高回滚一致性。
- **结构体命令与快照：** 输入命令、模拟状态和网络预测数据使用结构体表达，便于复制、恢复和重放。
- **状态与回调复用：** 状态注册表、动画回调缓存和对象池减少高频实例化及临时分配。
- **模拟与表现分频：** 权威运动保持固定 Tick，远端动画、IK、音频和 LOD 表现可以独立降频或关闭。

---

## 技术栈

- Unity 6.0，最低版本 `6000.0.0`。
- C# 与 Unity Assembly Definition。
- FishNet Prediction、Replicate/Reconcile 与 Tick 系统。
- Unity Input System 与 `CharacterController`。
- Animancer。
- Cinemachine。
- Unity Animation Rigging 与 RootMotion Final IK。
- ScriptableObject 配置、纯数据模拟、固定 Tick、状态快照和预测回滚。
- Root Motion 曲线采样与 Motion Warping。
- NiumaCore Runtime。

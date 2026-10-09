# NiumaTPC

## Network-Ready Action Controller Framework

**NiumaTPC** 是一个意图驱动、管线化的 Unity 第三人称动作控制器框架。它以“数据中心黑板 + 分层状态机 + 仲裁器 + 固定 Tick 模拟”组织移动、动作、装备和表现系统，并通过外观模式（Facade）与驱动器（Driver）解耦动画、IK、音频、相机和网络后端。

框架面向需要持续扩展的第三人称与动作类项目。当输入、状态切换、动画事件、装备、IK、音频和网络预测逐渐纠缠在一起时，NiumaTPC 将它们拆成职责明确、可以独立替换的层级，并让离线模式与联网模式复用同一套角色模拟规则。

## 脚本弹道与网络射击

射击采用“客户端提交意图、服务器模拟弹道、客户端播放表现”的结构。逻辑子弹由脚本按固定 Tick 推进，不依赖视觉预制体的 Rigidbody 速度或碰撞回调。射击请求从本地拥有者的相机准星采样，枪口用于枪焰与音效，不作为这条弹道的发射起点。

### 实现方式与解决的问题

| 实现方式 | 功能与作用 |
| --- | --- |
| `AmmunitionWorld` 与纯数据弹道状态 | 统一管理位置、速度、寿命、累计飞行距离和结束结果，避免每颗子弹各自推进刚体并决定命中 |
| 射线、球形扫掠与曲线子步 | 检查子弹运动路径与初始重叠，减少高速子弹跨帧越过障碍物的问题 |
| `WeaponDriver` 与 FishNet RPC | 服务器检查所有权、请求序号、装备代次、角色动作、弹量、冷却、时间窗和视点遮挡，客户端不直接决定实际发射 |
| Owner 即时表现与预测弹 | 请求提交时播放枪焰、枪声、后坐力及视觉子弹，减少等待网络往返造成的开火迟钝 |
| `ShotRequestKey` 与预测认领 | 用服务器武器实例、装备代次和请求序号关联预测弹；权威发射通知到达后复用视图并平滑校正，避免生成两颗视觉子弹 |
| 弹量快照与在途请求预算 | 客户端预占未确认请求的弹量，服务器回执更新预算，避免连续开火重复使用同一份剩余弹量 |
| `AmmunitionView.BindingId` 与对象池 | 将视图租用编号和服务器子弹编号分开，避免旧消息操作已经回收复用的对象 |
| 发射、结束与回执分工 | 远端根据确认通知播放开火；Owner 不重复播放回执表现，拒绝、结束或预测超时会清理对应视觉对象 |

`GunWeaponSO` 提供通用枪械资源，继承 `RangedWeaponSO` 的初速度、开火间隔、弹匣容量及弹药引用；`AmmunitionDefinitionSO` 提供弹药的重力、碰撞半径、寿命与视觉资源。武器运行时弹量和冷却保存在 `ItemInstance`，不是保存在可回收的枪械模型上。

### 场景接入

1. **角色对象：** 沿用移动接入的 `NiumaCharacterController` 与 `NiumaFishNetPredictionDriver`（文件 `PredictionDriver.cs`），在角色根 `NetworkObject` 同物体上挂 `WeaponDriver` 与 `OfflineWeaponFireSource`。采样器在这条链路中不启动离线弹道世界；`WeaponDriver._requestSource` 留空时查找同物体组件，采样使用 Owner 的 `Camera.main`，场景主相机必须属于本地玩家。
2. **出生装备：** 角色的 `DefaultEquipment1/2/3` 为服务器创建独立装备实例提供定义，空槽保持原位置，全部留空表示空手。客户端使用服务器同步的装备身份发射，而不是把本地表现对象作为权威装备记录。
3. **弹道世界：** 每个物理世界设置一个场景网络对象，同物体挂 `NetworkObject`、`AmmunitionDriver` 与 `AmmunitionPresenter`，并保证它对本局玩家可见。不要给每个玩家或视觉子弹各挂一个世界驱动，也不要同时运行 `OfflineAmmunitionDriver` 推进同一条发射流程。
4. **对象池与弹药目录：** `AmmunitionPresenter._visualPool` 绑定场景 `SimpleObjectPoolSystem`；`_ammunitionDefinitions` 填本局全部可用弹药 SO，不是某个玩家的武器列表。每个弹药 `DefinitionId` 必须非空且唯一，两端配置 ID 与资源映射保持一致。
5. **视觉子弹：** 弹药的 `VisualPrefab` 根节点挂 `AmmunitionView`，TrailRenderer 引用可以留空；它只跟随脚本计算的轨迹，不作为权威碰撞或伤害入口。
6. **命中特效：** `ImpactPrefab` 根节点挂 `AmmunitionImpactView`，`particleRoot` 留空可用于静态弹坑，`retainTime` 默认 5 秒，`surfaceOffset` 默认 0。未配置对应 Prefab 时不生成该表现，`ImpactSound` 留空时不播放一次性命中音效。特效自带 AudioSource 的音频和循环配置由 Prefab 自己管理。
7. **枪械表现：** `AK47Behaviour` 的 `_muzzle` 绑定视觉枪口，`_leftHandGoal` 绑定左手 IK 握点。`GunWeaponSO.MuzzleVFXPrefab` 和 `ShootSound` 可留空；每把已装备的枪复用一份池化枪焰，卸载或停用时回收。枪焰粒子使用 `Loop=false`、`PlayOnAwake=false`、`StopAction=None`，不挂自毁脚本、子弹视图或 NetworkObject。

### 弹道与网络配置

| 配置 | 含义 |
| --- | --- |
| `RangedWeaponSO.ProjectileSpeed` | 武器提供的子弹初速度，单位 m/s |
| `RangedWeaponSO.FireRate` | 两次开火的间隔，单位秒，不是每秒发数 |
| `RangedWeaponSO.MaxAmmo` / `Ammunition` | 弹匣容量与使用的弹药配置 |
| `AmmunitionDefinitionSO.CollisionRadius` | 0 使用射线，大于 0 使用球形扫掠 |
| `GravityScale` / `MaxLifetime` | 重力倍率与最大存活秒数；重力倍率为 0 时不下坠 |
| `AmmunitionDriver._hitMask` | 权威弹道可命中的层，包含墙体、地形和目标碰撞体 |
| `_triggerInteraction` | 默认 Ignore；使用 Trigger 受击框时设为 Collide |
| `AmmunitionPresenter._visualBlockMask` | 只选服务器也会阻挡子弹的静态墙体与地形；Nothing 关闭画面遮挡检查，不改变服务器命中 |
| `_predictionTimeout` | 默认 2 秒，等待权威发射通知的最长时间；超时只清理预测画面，不自行释放弹量预算 |
| `_confirmationBlendTime` | 默认 0.08 秒，预测弹认领后消除视觉位置误差的时间；0 表示立即校正 |
| `_maxActiveImpacts` | 默认 128，超限回收最早的特效；0 不生成命中特效 |
| `WeaponDriver._maxRequestAge` / `_maxRequestFutureLead` | 默认 0.75 / 0.1 秒，限制请求落后及领先服务器的时间，不是历史回溯窗口 |
| `_maxAimOriginDistance` | 默认 4 米，射击视点与服务器角色胶囊中心的最大距离 |
| `_maxAimOriginLateralDistance` / `_maxAimOriginForwardDistance` | 默认 2 / 0.5 米，限制视点相对胶囊中心的侧向及前向偏移，容纳合法肩位视角 |
| `_aimProbeRadius` / `_aimObstructionMask` | 默认探测半径 0.05 米；遮挡层须包含实体墙体与地形，不得为 Nothing，查询排除射手自身并忽略 Trigger |
| `_logRequests` / `_logEnds` | 默认开启，分别打印请求处理与子弹结束信息，可按排查需要关闭 |

### 职责与使用边界

- `IWeaponFirePresentation` 只处理枪械表现，不发送请求、不生成权威子弹、不扣弹；后坐力只影响本地 Owner 的后续视角，不修改已经采样的本发射线
- 实际命中由服务器弹道决定，客户端静态遮挡只控制飞行画面；伤害结算、装填和历史回溯不属于这里已提供的射击能力
- 装备身份以服务器出生槽位记录为基础；动态库存映射、背包换槽和装备切换授权需要由游戏装备流程对接，不由客户端自行确认
- 枪械模型不匹配时跳过该次表现，不强制切换装备；新加入客户端不补发此前的在途子弹
- 枪械发射要求运行网络角色与弹道世界；本地单人射击也需要 Host，组件本身不负责自动启动 Host。缺少发送组件或断线时不会回退离线发射
- 仓库提供模块源码与接入说明，使用项目需准备匹配的角色配置、动画、枪械和特效资源；Host 与客户端应使用兼容的 RPC 代码及配置

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
- 滑铲固定 Tick 位移、速度衰减、最短持续时间与跳跃输入缓冲；跳跃继承剩余水平速度，低速或撞墙结束滑铲

### 角色表现

- 全身、上半身和覆盖动作分层播放。
- 动画淡入淡出、速度控制、归一化时间、左右脚相和结束回调。
- 瞄准、手部 IK、翻越 IK 与不同 IK 后端适配。
- 相机跟随、自由观察、瞄准视角以及本地网络角色自动绑定。
- 装备、背包、武器行为、音效请求和对象池基础能力。

### 角色数据与选择

- 角色定义、基础属性、展示资源和游戏预制体分别配置，通过目录服务按角色标识查询
- 选角会话、队伍席位与确认状态分离，支持队伍重复角色限制和选择结果锁定
- 角色预览和正式角色生成使用独立入口，界面展示不承担角色权威运动

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

- `PlayerSO.Core.UpperBodyMask` 决定第 1 层装备动画覆盖哪些部位。持枪走跑可使用只启用双臂、手指和手部 IK 的 Humanoid 遮罩，关闭 Body、Head、Root、双腿和足部 IK，让躯干继续参与移动动画；遮罩资源由使用项目提供
- 躯干继续播放第 0 层的走跑动作，保留迈步时的躯干反向调整。将 Body 纳入静态持枪层可能覆盖这部分动作，导致胸口和枪跟着骨盆一起左右侧倾。正常的跑步转体仍会保留，这个配置并不是锁死上半身。
- `AimIK / Solver / Bones` 可将骨盆权重设为 0，保留脊柱参与瞄准，减少枪口校正直接扭动下半身；这不代表删除 `FullBodyBipedIK` 的骨盆引用，具体权重需按角色骨架和动画调整
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

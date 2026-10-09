# NiumaTPC

Unity 第三人称动作与网络角色控制框架

**NiumaTPC** 将输入、角色运动、动作切换、装备和画面表现拆成独立职责。核心使用输入管线、运行时黑板、分层状态机与仲裁器组织角色行为，并通过固定 Tick 模拟和 FishNet 适配层支持客户端预测、服务器复算与网络校正。

框架覆盖移动、跳跃、翻滚、闪避、翻越、滑铲以及网络枪械射击。动画、IK、相机和音频通过 Facade、Driver 与适配接口接入，让新增动作或更换表现后端时，不必同时改动输入、模拟和网络传输逻辑。

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

## 核心功能

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

### 枪械与弹道

- 准星射线采样、服务器脚本弹道与射线／球形扫掠碰撞
- 本地开火反馈、预测弹认领、视觉误差平滑与服务器弹量同步
- 枪焰、飞行子弹、命中特效和弹坑的对象池复用及回收

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

### 8. 脚本弹道与网络射击

射击采用“客户端提交意图、服务器模拟弹道、客户端播放表现”的结构。逻辑子弹由脚本按固定 Tick 推进，不依赖视觉预制体的 Rigidbody 速度或碰撞回调。射击请求从本地拥有者的相机准星采样，枪口用于枪焰与音效，不作为这条弹道的发射起点。

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
- **网络射击：** `WeaponDriver` 处理请求与装备身份，`AmmunitionDriver` 推进权威弹道，`AmmunitionPresenter` 处理预测弹与确认表现
- **角色数据：** `Runtime/Role` 提供角色定义、目录查询、选角会话和预览能力，展示对象与正式角色生成分开

---

## 性能设计

- **单入口驱动：** 角色帧更新和固定 Tick 模拟由少数明确入口统一调度，减少散落 `Update` 带来的调度与排查成本。
- **纯数据模拟：** ScriptableObject 在启动时转换为只读运行配置，模拟 Tick 不执行反射扫描，也不重复查询资源配置。
- **预采样运动曲线：** 起步、翻滚、闪避和翻越所需曲线提前转换为固定 Tick Profile，降低运行时求值差异并提高回滚一致性。
- **结构体命令与快照：** 输入命令、模拟状态和网络预测数据使用结构体表达，便于复制、恢复和重放。
- **状态与回调复用：** 状态注册表、动画回调缓存和对象池减少高频实例化及临时分配。
- **模拟与表现分频：** 权威运动保持固定 Tick，远端动画、IK、音频和 LOD 表现可以独立降频或关闭。

---

## 依赖说明

项目使用 Unity 6，开发基准为 `6000.0.75f1`，包清单最低版本为 `6000.0.0`。使用项目需自行准备角色模型、动画和特效资源，以及第三方插件的合法授权。

| 依赖 | 用途 |
| --- | --- |
| NiumaCore Runtime | 模块与通用运行时基础能力 |
| Unity Input System | 玩家输入采集，不使用旧输入系统 |
| Animancer | 动画外观层的分层播放、过渡、回调和事件 |
| Cinemachine | 第三人称相机与视角管理 |
| Unity Animation Rigging、RootMotion Final IK | IK 适配实现与目标、权重控制 |
| Universal Render Pipeline | Runtime 程序集引用的渲染管线依赖 |
| FishNet | 独立网络适配层的 Tick、预测、校正、RPC 和对象同步 |

动画、IK 和相机虽然通过接口组织，但随仓库提供的 `NiumaTPC.Runtime.asmdef` 直接引用上述 Runtime 依赖，不能仅因某个功能没用到就省略对应程序集。替换后端时，需同时处理适配实现与程序集引用；FishNet 代码单独放在 `NiumaTPC.FishNet.Runtime`，编辑器工具放在仅供 Editor 使用的程序集。

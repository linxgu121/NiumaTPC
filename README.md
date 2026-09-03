# NiumaTPC

NiumaTPC 是面向 Unity 6 的第三人称角色控制器。当前版本以固定 Tick、可回滚角色模拟为核心，并提供 FishNet 客户端预测与服务器权威同步适配。

## 功能

### 第三人称移动

- 待机、行走、慢跑与冲刺。
- 八方向起步、循环移动、停止和快速反向处理。
- 自由视角与瞄准移动模式。
- 基于 `CharacterController` 的碰撞、接地、重力和空中控制。

### 身体动作

- 普通跳跃、差异化起跳速度、二段跳、下落与多级落地表现。
- 翻滚与闪避，支持独立配置位移、持续时间、进度曲线和重力规则。
- 低位与高位翻越，包含环境探测、固定 Tick 轨迹、目标对齐与功能开关。
- 滑铲输入、输入缓冲和配置结构；滑铲权威运动与表现状态仍在开发。

### 输入与状态管理

- 使用 Unity Input System 采集玩家输入。
- 输入采集、输入处理、网络命令构建和角色模拟相互分离。
- 支持跳跃、翻滚、闪避、翻越和滑铲等动作的输入缓冲。
- 全身状态与上半身状态独立组织。
- 通过状态拦截器和动作仲裁处理跳跃、下落、落地、翻越、闪避等优先级与打断关系。

### 固定 Tick 角色模拟

- 离线模式与联网模式共用同一套角色模拟逻辑。
- 输入命令、模拟状态和表现状态使用独立数据结构。
- 移动、重力、跳跃、动作位移和翻越轨迹按固定 Tick 推进。
- ScriptableObject 配置在启动时转换为模拟层纯数据，避免模拟过程直接依赖资源对象。
- 支持保存、恢复和重放角色模拟状态，为预测回滚提供基础。

### FishNet 网络同步

- 本地拥有者输入采集与客户端预测。
- 服务器权威模拟与输入所有权校验。
- FishNet Replicate/Reconcile 回滚校正。
- 远端角色状态快照与动画表现同步。
- 网络角色摄像机仅绑定本地拥有者。
- 摄像机可跟随 `NetworkTickSmoother` 图形节点，隔离固定 Tick 模拟根节点造成的抖动。

### 动画与运动表现

- 使用 Animancer 管理动画播放、分层、淡入淡出、结束回调和动作相位。
- 权威模拟与动画表现解耦，远端角色不依赖本地状态机重新计算权威结果。
- 支持起步速度曲线、旋转曲线和左右脚相同步。
- 提供普通 Root Motion 与 Warped Motion 编辑器烘焙工具。
- 支持 Animation Rigging 与 Final IK，用于瞄准、手部 IK 和翻越对齐。

### 模块化配置

- 使用 `PlayerSO` 聚合移动、跳跃、瞄准、翻滚、闪避、翻越和滑铲配置。
- 玩法数值、动画资源、动画过渡参数和烘焙数据按功能模块拆分。
- 核心运行时、编辑器工具和 FishNet 适配层使用独立程序集，降低网络框架与角色逻辑之间的耦合。

## 技术

- Unity 6.0，最低版本 `6000.0.0`。
- C# 与 Unity Assembly Definition。
- FishNet 客户端预测、服务器权威、Replicate/Reconcile 和 Tick 系统。
- Unity Input System。
- Unity `CharacterController`。
- Animancer。
- Cinemachine。
- Unity Animation Rigging。
- RootMotion Final IK。
- ScriptableObject 数据配置与运行时纯数据快照。
- 固定 Tick 模拟、输入命令量化、状态快照、预测回滚和表现层桥接。
- Root Motion 曲线采样、动作轨迹烘焙与 Motion Warping。
- NiumaCore Runtime 基础模块。

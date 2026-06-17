# NiumaTPC

## 模块定位
NiumaTPC 是玩家第三人称控制模块，负责移动、旋转、跳跃、冲刺、翻越、输入启停、角色状态恢复，以及玩家位置存档适配。它不负责任务、交互、剧情、战斗伤害公式。

## 框架设计思路
- 以 PlayerModuleController 作为外部入口，内部角色控制实现对外隐藏。
- 内部运动与动作组织采用 HFSM（分层有限状态机）思路，而不是把 Idle、Run、Jump、Vault 等状态全部平铺。
- 对外提供稳定的输入启停接口，供 UI、剧情、场景加载、死亡复活等流程冻结玩家控制。
- 动画、IK、状态恢复等属于 TPC 内部实现，外部只调用方法，不读取内部细节。
- 玩家移动变化不直接作为存档脏标记来源，检查点/场景切换等流程显式触发保存。

## 核心流程
1. 输入源读取移动、视角、跳跃、冲刺等意图。
2. 角色控制器计算位移、地面检测、翻越与动画参数。
3. HFSM 将角色状态分为 Grounded、Airborne、Action、Disabled 等上层状态，再在内部处理 Idle、Run、Jump、Vault 等具体状态。
4. PlayerModuleController 对外暴露 DisableControl / EnableControl / Teleport 等方法。
5. 场景加载或剧情开始时冻结输入，结束后由调用方按状态恢复。
6. NiumaTPCSaveAdapter 导出位置、旋转、场景 ID 等快照。

## 模块用法
- 场景中的玩家物体挂载 TPC 控制脚本和 PlayerModuleController。
- 其他模块不要直接操作具体输入源，统一通过 PlayerModuleController 冻结/恢复。
- 其他模块不要直接切换 TPC 内部 HFSM 状态；需要移动、禁用、传送或复活时只调用 PlayerModuleController。
- 存档读档时先由场景流程确保目标场景加载完成，再调用 Teleport 恢复位置。

## 场景使用方法
推荐放置方式：`PlayerRoot` 一个玩家根物体承载 TPC 功能集，摄像机和存档桥接可作为子物体拆分。

- `PlayerRoot`：挂 `PlayerModuleController`，作为外部模块冻结输入、传送、状态恢复的统一入口。
- `PlayerRoot`：挂 `NiumaCharacterController` 以及角色碰撞体、Animator、输入源等 TPC 内部控制组件。
- `PlayerRoot/CameraRig`：挂 `PlayerCameraManager`、`CameraRigDriver`，绑定主相机和跟随点。
- `PlayerRoot/SceneBridge`：需要场景恢复玩家位置时，挂 `TPCSceneSpawnTarget` 和 `TPCSceneInputBlockTarget`，给 NiumaScene 调用。
- `PlayerRoot/UIBridge`：需要 UI Toolkit 打开菜单/对话/Loading 时冻结玩家输入，挂 `TPCGameplayInputBlocker`，拖给 `UIToolkitUIManager.Input Blocker Provider`。
- `PlayerRoot/SaveAdapter` 或全局 `SaveRoot`：挂 `NiumaTPCSaveAdapter`，绑定 PlayerModuleController 和场景 ID 来源。
- `ObjectPoolRoot`：如果使用角色池或特效池，单独挂 `SimpleObjectPoolSystem`，不要混在玩家根物体里。
- NPC 或其他角色若只需要动画展示，不建议挂完整 TPC；需要移动控制时再拆出独立 CharacterRoot。

## 协作边界
NiumaScene 负责场景跳转，NiumaSave 负责快照保存，NiumaAttribute 可逐步接管 Health/Stamina 等数值事实。TPC 保持“控制玩家身体”的职责，不扩展成战斗或属性模块。

## 场景挂载与 Inspector 配置
### PlayerModuleController
建议挂载位置：`PlayerRoot`。

用途：玩家控制模块门面，供 Scene、Save、Attribute、Audio 等模块调用控制启停、传送和状态查询。

| 字段 | 怎么填 | 可否留空 | 不填会怎样 |
| --- | --- | --- | --- |
| `Character Controller / Motor` | 绑定玩家移动主体 | 不可以 | 玩家无法正常移动 |
| `Input / Camera / Animator` | 按 TPC 预制体实际组件绑定 | 不建议 | 对应控制、相机或动画功能失效 |
| `Use External Sfx Bridge` | 接入 NiumaAudio 时开启 | 可以 | 关闭时仍使用 TPC 原本本地音效 |

### TPCSceneSpawnTarget
建议挂载位置：`PlayerRoot/SceneBridge`。

| 字段 | 怎么填 | 可否留空 | 不填会怎样 |
| --- | --- | --- | --- |
| `Player Controller` | 拖 `PlayerModuleController` | 不建议 | 场景切换后无法把玩家放到 SpawnPoint |
| `Auto Resolve Player Controller` | 单玩家测试可开，多角色正式场景建议关 | 可以 | 关闭且未绑定时不工作 |

### TPCSceneInputBlockTarget
建议挂载位置：`PlayerRoot/SceneBridge`。

| 字段 | 怎么填 | 可否留空 | 不填会怎样 |
| --- | --- | --- | --- |
| `Player Controller` | 拖 `PlayerModuleController` | 不建议 | 加载期间不能冻结玩家控制 |
| `Auto Resolve Player Controller` | 单玩家测试可开 | 可以 | 多玩家场景自动查找可能找错 |

### TPCAudioBridge
建议挂载位置：`PlayerRoot/AudioBridge`。

| 字段 | 怎么填 | 可否留空 | 不填会怎样 |
| --- | --- | --- | --- |
| `Player Controller` | 拖 `PlayerModuleController` | 不建议 | 接收不到跳跃、落地、受伤等事件 |
| `Audio Controller` | 拖 `NiumaAudioController` | 不建议 | 无法通过 NiumaAudio 播放 3D 音效 |
| `Cue Mapping` | 配置 PlayerSfxEvent 到 CueId 的映射 | 不可以 | 事件触发但没有对应声音 |

### NiumaTPCSaveAdapter
建议挂载位置：`CoreScene/BootstrapRoot/SaveRoot/SaveAdapters` 或玩家常驻根。

| 字段 | 怎么填 | 可否留空 | 不填会怎样 |
| --- | --- | --- | --- |
| `Player Controller` | 拖 `PlayerModuleController` | 不建议 | 玩家位置/旋转不存档 |
| `Save Controller` | 拖 `NiumaSaveController` | 不建议 | 无法注册存档 Provider |



## 配置资产粒度基准

NiumaTPC 的配置资产主要服务角色控制、HFSM、动作和输入，不作为通用业务内容资产基准。

- `PlayerSO`、`PlayerBrainSO`、各类动作 / 移动 / 跳跃 / 翻滚 / 瞄准等 SO：按角色控制方案或动作模块拆分。
- StateInterceptorSO / UpperBodyInterceptorSO：按拦截规则拆分，不要混入任务、对话、物品等业务内容。
- 武器或角色动作配置如果与 Inventory / Equipment / Skill 有关联，应通过稳定 ID 或桥接层协作，不要让 TPC 反向承载业务事实。

TPC 负责动作仲裁和表现参数；任务、对话、数值、技能、效果等配置仍放在对应模块。

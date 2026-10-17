# Player：三态 ECS + MVC

## 数据与形态

所有玩家 Component 放在 PlayerComponent.cs。PlayerSolidComponent、PlayerLiquidComponent、PlayerGasComponent
各保存对应形态参数，均为普通 IComponentData。
PlayerFormComponent.Value 是当前形态的唯一权威状态；读取时直接读取枚举字段。
PlayerSystem.SetForm 在切换入口校验并写入枚举，不使用形态启用位。
位置、实际 Velocity 和输入由共用 Component 保存，切换不会重建 Entity 或重置位置。

PlayerSystem 在固定模拟步查询玩家共用状态，按枚举分支只读取当前形态参数，每步只积分一次。
固态具有加减速响应，液态直接响应输入，
气态叠加环境风。地图 XY 与独立 Height 分开；固液形态受重力下落，落地高度归零。
俯视模式的气态高度只随 WindVelocity.z 改变，W/S 只改变影子代表的地面 XY；PhysicsSystem 将高度限制在环境的 maximumHeight 内。
PlayerView.heightDisplayScale 配置每米玩法高度对应的显示单位数，默认 1；Tutorial01 使用 2，使 2m 天花板对应 4 个显示单位。
主体锚点显示 Y = min(XY.y + Height * heightDisplayScale, maximumDisplayY)，影子与碰撞仍留在地面 XY。
PlayerView.maximumDisplayY 单独配置主体显示锚点的场景 Y 上限，默认 float.MaxValue。到顶后继续按 W，影子向上移动而主体锚点保持封顶。
显示限制用于所有形态，转换为液态时保留高度并连续下落；显示位置不回写 ECS，也不用于交互和碰撞。
PlayerSystem 将期望位移交给统一 PhysicsSystem，使用返回的允许位移和 Velocity 更新 ECS。
PhysicsSystem 从 Unity Collider 查询碰撞、限制移动、处理沿边滑动，并输出接触实体。
ObstacleSystem 在物理查询后处理 Interact 请求；只有 Solid 能破坏接触到的可破坏障碍物。
Interact 默认为 E，不另设破坏按键或请求字段。当前未增加玩家主动切换形态的按键。

速度术语统一使用 Velocity；标量配置用 MoveVelocityMagnitude，实际向量用 Velocity。
三个 ScriptableObject 配置位于 Assets/_Game/Data：
PlayerSolidConfig.asset、PlayerLiquidConfig.asset、PlayerGasConfig.asset。
初始化时复制到 ECS，运行中修改配置资源不会自动改写实体。
要求所有数值为有限正数，且气态 < 固态 < 液态；固态 ResponseTime 为正。

## 门控调用

GameBootstrap 是总入口，在 Start 中统一初始化输入、环境、障碍物和玩家，关闭时逆序回收。
各门控通过显式 Initialize(...)/Shutdown() 开启和关闭，不自行启动。

```csharp
var inputSettings = inputBootstrap.Initialize();
environmentBootstrap.Initialize();
foreach (var obstacle in obstacleBootstraps) obstacle.Initialize();
playerBootstrap.Initialize(inputSettings);

// 关闭时逆序，错误由 Unity 直接报告。
playerBootstrap.Shutdown();
for (int i = obstacleBootstraps.Length - 1; i >= 0; i--) obstacleBootstraps[i].Shutdown();
environmentBootstrap.Shutdown();
inputBootstrap.Shutdown();
```

初始化由总入口在启动时调用一次，各门控不维护初始化防重状态。
总门控负责统一关闭，不再由每个组件的 OnDestroy 重复关闭。
初始化失败直接抛出异常，不自动回滚；停止运行时根据实际资源状态释放已创建资源。
玩家门控引用三份配置、出生形态、出生高度、Controller、View、输入设置、Kinematic Rigidbody2D、
三种形态的 Collider 和 collisionLayers。Collider 节点独立于 bodyRoot，不随表现动画改变大小或高度。
环境门控单独配置 WindVelocity 和 Gravity，使用框架创建的默认 ECS World。
PlayerBootstrap 在 Controller 关闭、物理注册解除后才销毁玩家实体。

## 统一 Player Prefab

所有含 Player 的项目场景均使用 `Assets/_Game/Prefabs/Gameplay/Player.prefab` 的实例。
模板以 Tutorial01 的 Player 为准，包含三态表现、阴影、伤害场、Rigidbody2D、碰撞节点和玩家脚本。
共享外观、组件引用、输入 Action 和默认参数在 Prefab Mode 中修改；各场景保留自己的出生位置。
需要场景差异时，可覆盖初始形态、出生高度或伤害场数值，避免覆盖共用表现节点。

Prefab 不引用场景输入对象。InputBootstrap.Initialize 返回本场景已初始化的 InputSettingsController，
GameBootstrap 将它传给 PlayerBootstrap.Initialize；输入副本仍由原输入管理器统一持有和释放。
GameBootstrap.playerBootstrap 引用场景中的 Prefab 实例。新场景拖入同一个 Prefab 后配置该引用即可。
这是各场景共用模板，Player 仍由各场景的门控创建和销毁，不引入跨场景常驻对象。

Tutorial01、PlayerMovementTest 和 ObstacleTest 均已接入总入口，并按对象归属挂载：

```text
Game        → GameBootstrap
Player      → PlayerBootstrap、PlayerController、PlayerView
Environment → EnvironmentBootstrap
Input       → InputBootstrap、InputSettingsController
```

PlayerController 与 PlayerView 跟随 Player 对象启停，不挂在总入口对象上。

## View 与输入

PlayerView 初始化时校验 bodyRoot、三态 SpriteRenderer、LiquidView、气态 ParticleSystem 及其 Renderer，
缺失立即报错。初始化直接应用出生形态；shownForm 只缓存上次显示结果，
避免反复重启粒子。Refresh 不判空，不包含玩法规则。

PlayerController 在场景中默认禁用，由 Initialize() 开启，Shutdown() 关闭并解除事件订阅。
不维护 initialized 标志，不通过 Awake()/OnEnable() 自动装配。
Controller 关闭时调用 PlayerView.Shutdown，停止粒子并释放 LiquidView 持有的运行时网格。

液态以原 SpriteRenderer 的图片、颜色和材质为来源，在 Liquid/Surface 生成 24×10 格网格。
脸部区域保持稳定，外围根据 ECS 实际 Velocity 拉伸、延迟跟随并在启停和转向时短暂回弹，静止时轻微起伏。
不再将液态图片旋转到移动方向；网格和顶点数组复用，不修改原始 Sprite、Collider 或 ECS 状态。
LiquidView 的 flowStretch、edgeRipple、responseTime 分别调整拉伸、边缘波动和跟随时间。

气态使用 Gas.png 的 Sprite 主体，附带轻微呼吸和浮动。Gas Particles 在世界空间按路程发射，
停下后保留少量逸散；烟雾使用 GasSmoke 材质，逐渐扩散、上浮和淡出。原 Gas Trail 已移除。
形态切换和重生清理表现缓存；阴影跟随各形态实际显示范围。
ConverterTest 直接继承共享 Player Prefab，可在四个转换点按 E 切换后观察静止、启停和转向。
Inspector 中的 moveAction、interactAction 和 showDamageFieldAction 使用 InputActionReference，
分别选择 Player/Move、Player/Interact 和 Player/ShowDamageField；要求 Move 提供 Vector2，其他两个为 Button。

伤害场是 Player 的能力：DamageFieldComponent 与位置、形态等数据保存在同一玩家实体上，
PlayerSystem 在移动完成后执行伤害场扣减。PlayerController 读取 Alt 并调用 PlayerView 显示玩家子节点。
没有独立的伤害场实体、Controller、View、Bootstrap、System 或 Prefab。
外观保存为共享圆环 Mesh、材质和空心圆贴图；具体配置与验收见同目录 DamageField.md。

InputSettingsController 创建唯一运行时 InputActionAsset 副本，
按 Inspector 引用的 Action ID 直接解析并缓存对应动作，避免 GUID 转字符串。
改键与玩家输入使用同一个副本。
GetBindingCount、GetBinding、GetBindingDisplayString、BeginRebind 均接收 InputActionReference，
不再依赖动作名称字符串。保存键仍为 Emergence.Player.Bindings.v1。
改键时禁用输入，Esc 取消，支持保存、加载和恢复默认。改键不暂停 ECS 模拟。
改键启动或存档解析失败直接抛出异常，不捕获后静默恢复默认配置。

## 检查

菜单 Emergence > Checks > Player Movement 在独立测试 World 和临时物理场景中验证运动、形态分支和配置，
测试结束关闭临时场景，不修改已有场景；需在非 Play 模式运行。RunBatch 提供批处理入口。
测试覆盖原有运动行为，并检查每步只执行当前形态、三态参数隔离、非法形态及配置复制。
Emergence > Checks > Obstacle Destruction 验证 Unity 碰撞阻挡、离开、滑动、形态切换、Inspector 尺寸及破坏条件。
Emergence > Checks > Player Prefab 检查共享资源的引用归属、所有场景的 Prefab 连接、输入注入和初始化/关闭流程。

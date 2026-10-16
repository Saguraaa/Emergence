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
各门控只提供显式 Initialize()/Shutdown()，不自行启动。

```csharp
inputBootstrap.Initialize();
environmentBootstrap.Initialize();
foreach (var obstacle in obstacleBootstraps) obstacle.Initialize();
playerBootstrap.Initialize();

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

Tutorial01、PlayerMovementTest 和 ObstacleTest 均已接入总入口，并按对象归属挂载：

```text
Game        → GameBootstrap
Player      → PlayerBootstrap、PlayerController、PlayerView
Environment → EnvironmentBootstrap
Input       → InputBootstrap、InputSettingsController
```

PlayerController 与 PlayerView 跟随 Player 对象启停，不挂在总入口对象上。

## View 与输入

PlayerView 初始化时校验 bodyRoot、固液 SpriteRenderer、气态 ParticleSystem 及其 Renderer、
TrailRenderer，缺失立即报错。初始化直接应用出生形态；shownForm 只缓存上次显示结果，
避免反复重启粒子。Refresh 不判空，不包含玩法规则。

PlayerController 在场景中默认禁用，由 Initialize() 开启，Shutdown() 关闭并解除事件订阅。
不维护 initialized 标志，不通过 Awake()/OnEnable() 自动装配。
View 的表现对象随 GameObject 销毁，不额外实现重复关闭或缩放恢复。
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

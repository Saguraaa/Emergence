# NPC

## 第二阶段：NpcTest

打开 `Assets/_Game/Scenes/Test/NpcTest.unity`。玩家出生在 `(-6, -3)`，`NPC_A` 在 `(0, -3)`，`NPC_B` 在 `(-1, 4)`。WASD / 方向键移动，Alt 查看伤害场，靠近 NPC 即持续降低抵抗值，无需按 Alt。

- 正常：灰黄色，满抵抗值时待机。
- 警戒：橙色，抵抗值首次实际下降时进入。
- 逃跑：红色，剩余抵抗值 ≤ 初始抵抗值的 80% 时进入。一次扣减跨过阈值也会直接逃跑。
- 抵抗值下降时显示头顶条；最后一次下降后保留 1.5 秒。显示时长在 `NPCController` 中配置。
- 运行时选择 NPC 的 `NPCController`，Inspector 下方 `Runtime ECS` 可直接查看抵抗值、行为、路线阶段、当前目标下标（从 0 开始）和剩余等待时间。

`NPC_A` 的路线是 `(2,-3) → (2,2) → (6,2) → (8,0)`，分别停留 1、0.5、1.5、2 秒，绕过 `Route Wall`。`NPC_B` 有另一条独立路线，停留 0.5、1、2 秒。

## Prefab 与配置

`Assets/_Game/Prefabs/Gameplay/NPC.prefab` 包含独立碰撞节点、Kinematic Rigidbody2D、SpriteRenderer、头顶条、Controller、View 和 Bootstrap。Collider 默认关闭，Controller 默认禁用，由门控开启。Collider 的尺寸和偏移只在 Collider Inspector 中配置。

将 Prefab 放进场景后：

1. 在独立场景节点下创建路径点，不放在移动的 NPC 层级内部。
2. 在 `NPCBootstrap.Waypoints` 中按顺序拖入 Transform，并填写各点 `Wait Duration`；至少一个点。
3. 将 NPCBootstrap 加入 `GameBootstrap.Npc Bootstraps`。
4. 按需设置 `Move Velocity Magnitude`、`Initial Resistance`、`Flee Threshold Ratio`、`Arrival Distance` 和 `Blocked Duration`。

路径坐标和等待时间在初始化时复制到 ECS；运行中移动编辑用 Transform 不会修改路线。Prefab 资源本身不引用场景路径点，空路线在实例初始化时会明确报错。

## 行为规则

`NPCSystem` 使用固定优先级决策与状态机：逃跑 > 警戒 > 正常。主循环只组织 `SelectBehavior → ChangeBehavior → TickBehavior → Move`，分别负责选择行为、处理切换、执行行为和受约束移动，不引入通用行为树框架或图形工具。

`ChangeBehavior` 仅在刚进入逃跑时重置路线。`TickFlee` 按 Moving / Waiting / Completed / Blocked 分发，分别调用前往路径点、等待或保持停止；每到一点等待一次，最终停留，不循环、不折返、不自动恢复抵抗值。归零暂不附加死亡或销毁效果。

所有移动经过同一个 `PhysicsSystem`。相邻路径点之间必须允许碰撞箱通过；转弯需要显式放点。持续没有接近目标达到 `Blocked Duration` 后，路线进入 `Blocked` 并停止，不跳点、不传送；调整路线后重新运行。终点状态为 `Completed`，仍保留 `Fleeing` 行为。

## 攻击入口

`NPCController.On Attack` 是可序列化 UnityEvent。空事件（含 null、没有启用的 Inspector 回调）表示没有攻击能力。挂上攻击组件并绑定其公开方法后，可调用 `NPCController.RequestAttack()` 提交命令；System 记录请求，Controller 每条调用一次回调。插件中的伤害等玩法规则应通过对应 System 执行。

本次不增加自动攻击条件、攻击组件或攻击效果，也不进行攻击手段验收。运行中动态增删事件监听不在当前功能范围内；能力由初始化时的 Inspector 配置决定。

## 代码与数据检查

菜单 `Emergence/Checks/NPC` 在隔离的临时 World 中检查距离衰减、步长一致性、零值限制、行为阈值、路径点顺序与等待、终点停留、受阻、物理接触和头顶条刷新，并读取两个已保存测试场景检查引用。不会进入 Play 模式，也不执行攻击效果。场景交互与视觉验收由使用者完成。

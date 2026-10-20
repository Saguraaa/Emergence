# 玩家伤害场

## 第一阶段：AttackTest

打开 `Assets/_Game/Scenes/Test/AttackTest.unity`，运行后用 WASD / 方向键移动。按住左 Alt 或右 Alt 显示伤害场，松开隐藏。场内是稀疏淡绿色空心圆粒子，边缘是深绿色描边。场景保留四个形态转换点，可按 E 转换形态，对照三个形态的范围。

- `PlayerBootstrap` 的 `Damage Field Radius`、`Damage Field Strength` 提供初始半径和每秒强度。运行中的权威数值保存在同一个玩家实体的 `DamageFieldComponent` 中，类型位于 `PlayerComponent.cs`。
- `Player/DamageField` 是 Player 根节点的直接子节点，内含描边 MeshRenderer 和空心圆 ParticleSystem，不挂独立脚本。它随 `Player.prefab` 一起保存，没有独立的伤害场 Prefab。
- `Assets/_Game/Data/DamageField/Outline.asset` 保存单位半径的圆环 Mesh。材质保存在同目录，空心圆贴图在 `Assets/_Game/Art/DamageField/HollowCircle.png`。外观无需运行时重建；`PlayerView` 根据 ECS 半径缩放。
- `PlayerView` 通过 Inspector 引用 `Damage Field Root`、`Damage Field Outline` 和 `Damage Field Particles`，由现有 `PlayerBootstrap` 和 `PlayerController` 初始化、刷新与关闭。
- `PlayerController.Show Damage Field Action` 引用输入资源的 `Player/ShowDamageField`，通过现有输入管理器按 Action ID 解析运行时副本。Alt 只控制显示，不写入一份重复的 ECS 显示状态，也不控制伤害。
- 圆心跟随玩家地面根节点；不跟随 BodyRoot 的形态动画缩放或气态表现高度。

## 抵抗值扣减

每个固定步按玩家和 NPC 的 ECS 中心位置计算二维距离 `d`：

`扣减 = Strength × max(0, 1 - d / Radius) × DeltaTime`

圆心处扣减最快，边界及范围外不扣减；NPC 抵抗值最低为 0。玩家死亡期间不扣减。三个玩家形态共享同一份参数。

固定步顺序是 `PlayerSystem（移动后 ApplyDamageFields）→ NPCSystem → PhysicsSystem → InteractionSystem → ObstacleSystem`。伤害场使用 NPC 本步移动前的位置采样，阈值变化在同一步 NPC 决策时生效。

## 代码与数据检查

菜单 `Emergence/Checks/Damage Field` 检查 Player 输入引用与两侧 Alt 的绑定、共享单位圆环 Mesh、空心贴图、粒子配置、三个形态共用半径、玩家实体数据归属、PlayerView 引用与关闭流程。检查代码位于 `Gameplay/Player/Editor`。编辑模式不执行游戏 InputAction，真实按键和视觉效果由 AttackTest 的人工验收完成。

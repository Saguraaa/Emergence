# 交互点

`InteractionBootstrap` 在 Inspector 选择 `InteractionMode`：
- `InteractPressed`：玩家在范围内按 Interact（默认 E）触发，每次按下触发一次。
- `EnterRange`：每次进入触发一次，停留不重复，离开后可以再次触发。

范围以 `triggerRange` 的 Collider2D 为唯一来源，默认 prefab 使用半径 1.5 的 CircleCollider2D，开启 Is Trigger。
`interactionLayers` 过滤参与范围查询的 Layer，默认为 Actor。范围查询由 PhysicsSystem 完成，不使用 Unity 碰撞回调。
交互点默认不阻挡移动，三种玩家形态均可交互；多个交互点重叠时，每个符合条件的点都会响应。

`minimumHeight` 为最低交互高度，默认 0；`maximumHeight` 为最高交互高度，默认 float.MaxValue，保留旧场景无实际高度上限的行为。
高度范围包含两端；初始化要求两者有限、非负，且 maximumHeight >= minimumHeight。`allowedForms` 默认 All，可只勾选 Gas。
例如 minimumHeight=1.8、maximumHeight=2、allowedForms=Gas：玩家必须是气态且 1.8 <= Height <= 2 才满足条件。
这些参数初始化写入 InteractionRequirementComponent；二维范围仍由地面投影上的 triggerRange 决定。
高处交互点的精灵可以抬高，triggerRange 不随表现高度移动。
InteractionSystem 在二维重叠候选中筛选存活、形态和高度条件，只有满足全部条件才算占用。
EnterRange 因而支持在范围内原地升高或降低至允许高度后触发一次；离开高度范围或切为不允许的形态后，再满足条件可重新触发。

ECS 保存交互方式、范围占用状态和待处理触发次数，InteractionSystem 判断触发规则。
InteractionController 取出待处理次数并调用 Inspector 中的 `onActivated` UnityEvent，可绑定多个组件方法。
玩法状态的变化仍应通过对应 System 完成；InteractionTextOutput 是供验收使用的文本输出组件。
InteractionView 只负责 SpriteRenderer。GameBootstrap 显式装配 interactionBootstraps，关闭时先停止 Controller，再解除物理注册，最后销毁实体。

固定步顺序：PlayerSystem → PhysicsSystem → InteractionSystem → ObstacleSystem。
ObstacleSystem 在两个玩法读取者执行后清除共享 InteractPressed。

代码检查菜单：`Emergence/Checks/Interaction`。
场景：`Assets/_Game/Scenes/Test/ActivativeTest.unity`。玩家附近的交互点采用按键模式，onActivated 绑定 InteractionTextOutput.Output，输出 `ActivativeTest: interaction activated.`。
场景运行验收由用户完成。

## 场景传送与独立落点

Tutorial01 的 `InteractionPoint_FloorDrain` 使用 `EnterRange`，`allowedForms` 仅允许 Liquid。
`onActivated` 绑定同物体上的 `SceneTransferAction.Transfer`；目标场景通过 Inspector 拖入，编辑器自动保存运行时加载路径。
`destinationPointId` 指定目标场景内的落点 ID；`gameBootstrap` 绑定当前场景总开关。
SceneTransferPoint 配置 `pointId` 和任意本场景 GameObject 的 `anchor` Transform，同场景 ID 不得重复。
落点不属于 PlayerBootstrap，不依赖 Player 的摆放位置；缺失或重复落点直接报告配置错误。
Tutorial01 与 PipeMap 均须启用在构建场景列表中。

SceneTransferSystem 只在 ECS 请求中保存目标场景和落点，不保存形态或玩家状态副本。
默认 World 及同一个 Player 实体跨场景保留；门控在有传送请求时只关闭 Controller、解除物理注册，不销毁玩家。
目标门控绑定该实体，初始配置只在首次创建玩家时应用；传送只更新 XY，保留 Height、Velocity、形态、运行参数及额外组件。
输入解绑保留已有请求；物理接触缓冲属于旧场景查询结果，解绑时清空。
固定步规则在卸载前暂停，目标玩家、输入、物理和摄像机全部装配后恢复。
视角和运动模式由目标场景环境提供，恢复后的正常玩法仍会按该地图规则更新状态。
每张地图使用同一个 Player Prefab 的场景实例作表现和物理绑定，增加传送点不需要增加 Player。

Tutorial01 左侧地漏（Liquid / EnterRange）→ PipeMap 的 PipeLeft 锚点 `(-5.5, 16)`。
PipeMap 保留现有右侧 Liquid2Gas 转换点；气态升至右管道的 Interactive_ChangeSccene 后自动返回 TutorialRight。
TutorialRight 的 SceneTransferPoint 直接挂在 Tutorial01 右侧地漏上，Anchor 指向自身 Transform，返回形态自然保持 Gas。
直接启动地图仍使用该场景玩家的默认出生位置与初始配置。
检查入口：`Emergence/Checks/Scene Transfer`，覆盖实体存续、任意组件、速度、输入、启用状态、落点解析及绑定顺序。

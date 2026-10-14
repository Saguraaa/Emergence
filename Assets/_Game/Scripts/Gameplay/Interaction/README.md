# 交互点

`InteractionBootstrap` 在 Inspector 选择 `InteractionMode`：
- `InteractPressed`：玩家在范围内按 Interact（默认 E）触发，每次按下触发一次。
- `EnterRange`：每次进入触发一次，停留不重复，离开后可以再次触发。

范围以 `triggerRange` 的 Collider2D 为唯一来源，默认 prefab 使用半径 1.5 的 CircleCollider2D，开启 Is Trigger。
`interactionLayers` 过滤参与范围查询的 Layer，默认为 Actor。范围查询由 PhysicsSystem 完成，不使用 Unity 碰撞回调。
交互点默认不阻挡移动，三种玩家形态均可交互；多个交互点重叠时，每个符合条件的点都会响应。

ECS 保存交互方式、范围占用状态和待处理触发次数，InteractionSystem 判断触发规则。
InteractionController 取出待处理次数并调用 Inspector 中的 `onActivated` UnityEvent，可绑定多个组件方法。
玩法状态的变化仍应通过对应 System 完成；InteractionTextOutput 是供验收使用的文本输出组件。
InteractionView 只负责 SpriteRenderer。GameBootstrap 显式装配 interactionBootstraps，关闭时先停止 Controller，再解除物理注册，最后销毁实体。

固定步顺序：PlayerSystem → PhysicsSystem → InteractionSystem → ObstacleSystem。
ObstacleSystem 在两个玩法读取者执行后清除共享 InteractPressed。

代码检查菜单：`Emergence/Checks/Interaction`。
场景：`Assets/_Game/Scenes/Test/ActivativeTest.unity`。玩家附近的交互点采用按键模式，onActivated 绑定 InteractionTextOutput.Output，输出 `ActivativeTest: interaction activated.`。
场景运行验收由用户完成。

# Converter 形态转换回调

回调脚本位于 `Gameplay/InteractCallback/Converter/ConverterAction.cs`。将其挂到交互点对象，在 Inspector 的 `Conversion` 下拉列表选择固态 → 液态、液态 → 气态、气态 → 液态或液态 → 固态，然后将 `ConverterAction.Convert()` 绑定到 `InteractionController.onActivated`。

沿用 InteractionBootstrap 与 InteractionController，不需要转换门控、额外 ECS 实体或转换组件。InteractionSystem 和 InteractionController 不识别 Converter 类型。ConverterAction 参照 PlayerDeathAction 获取当前 World 的唯一玩家并调用 ConverterSystem；System 校验起始形态、死亡状态并通过 PlayerSystem.SetForm 修改 ECS 权威形态。转换不重置位置、高度或 Velocity。

onActivated 表示交互已触发，与转换是否成功无关。形态不匹配时不改变状态，事件中其他回调仍会执行。EnterRange 每次进入触发一次，停留期间不重试，离开再进入可再次触发。

场景：`Assets/_Game/Scenes/Test/ConverterTest.unity`。四个点从左到右为固 → 液 → 气 → 液 → 固，均为按 E 交互，各自只绑定一个 Convert 回调，玩家初始为固态。

代码检查菜单：`Emergence/Checks/Converter`，验证真实 Controller → UnityEvent → ConverterAction → System 链路、四种转换的全部起始形态、事件与转换结果独立、消费次数、按键及自动触发、死亡拒绝、位置和 Velocity 保持。交互回归检查：`Emergence/Checks/Interaction`。场景实际运行验收由用户完成。

# 重生点

- `RespawnPoint.prefab` 使用 `RespawnBootstrap` 初始化 ECS 位置与启用状态，Transform 只提供出生初值。
- `RespawnEnabledTagComponent` 是可启停 Tag，Inspector 的 `Initially Enabled` 决定初值。运行时由玩法 System 调用 `RespawnSystem.SetEnabled` 切换。当前只允许同时启用一个重生点；切换时先禁用旧点，再启用新点。
- 玩家初始化时添加可启停的 `DeadTagComponent`，默认禁用。死亡规则启用此 Tag，无血量数据。
- 交互点配置 `EnterRange` 模式，挂上独立脚本 `PlayerDeathAction`，在 `onActivated` 回调中拖入该组件并选择 `Die()`。该入口查找当前 World 中的唯一玩家，调用 `PlayerSystem.Die` 启用死亡 Tag；不依赖 PlayerController，交互点不保存死亡专用字段或 Tag。
- 固定步顺序：玩家移动 → 物理接触与范围采集 → 交互规则 → 障碍物规则 → 重生。交互 Controller 在 LateUpdate 发出回调，死亡标记由下一次固定步的重生 System 处理。重生通过统一物理入口同步位置并修正穿透，清空速度、输入与旧接触，保留当前形态。
- 没有启用的重生点时保留死亡 Tag 并停止移动；有启用点后自动重生。
- 重生 System 完成位置同步和状态清理后发出 `Respawned(Entity)` 事件。PlayerController 初始化时订阅、关闭时取消订阅，收到自身实体的通知后调用 View 播放 0.6 秒放大回弹动画，只影响 `BodyRoot` 表现节点，不影响碰撞节点。不保存复活计数。

## 检查与场景

- 菜单 `Emergence/Checks/Respawn`：隔离 ECS World 中检查区域死亡、禁用点、三形态重生、状态清理、物理同步和重复死亡。
- `Assets/_Game/Scenes/Test/RebithTest.unity`：一个红色自动死亡交互点、一个绿色启用重生点。死亡区域位于玩家出生点右侧 4 个单位，重生点位于出生位置。
- 已完成代码和保存数据检查；Play 模式下的操作及动画效果由用户验收。

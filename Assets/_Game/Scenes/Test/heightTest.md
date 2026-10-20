# heightTest 验收

打开同目录的 heightTest.unity 后 Play。场景使用固定镜头，玩家复用 Player 预制体；所有 Collider 由对应 Bootstrap 注册到统一 PhysicsSystem。

## 场景参数

- Environment：TopDown，maximumHeight=2m，WindVelocity=(0,0,0.5)，Gravity=9.81。
- 玩家：初始 Gas，Height=0；WASD 沿地面 XY 移动，无法手动改变高度。
- 本测试场景的玩家表现与碰撞尺寸使用共享预制体的四分之一，浴缸和淋浴头均大于玩家；玩家根节点保持单位缩放，高度位移仍按原比例显示。共享预制体不修改。
- Bathtub：不可破坏，obstacleHeight=0.6m，BoxCollider2D 配置地面占地。
- Shower：EnterRange，allowedForms=Gas，minimumHeight=1.8，maximumHeight=2；onActivated 调用 GasToLiquid 转换。
- 淋浴头的 CircleCollider2D 位于地面 XY，高处精灵独立显示。范围判断使用玩家当前形态的 Collider 与触发形状重叠；可在 Inspector 查看范围，场景中不添加文字、标题或触发范围标记。
- PlayerView.maximumDisplayY=2 是主体显示锚点的场景 Y 上限，与 2m 的 ECS 高度分别配置。主体精灵、呼吸动画及粒子可能超出锚点。
- 外围墙高 2m，到达天花板仍不能穿出房间。

## 手动验收

1. 出生后立即持续按 D：最初被浴缸挡住；约 1.2 秒后 Height 超过 0.6m，可以跨过。继续移动到淋浴头附近，满足 1.8～2m 时自动变为液态并下落。
2. 重新 Play，先停留约 4 秒：Height 停在 2m。按 W/S 只改变地面 XY，主体碰到显示上限后不再向上移动；按 S 也不会降低气态的实际高度。
3. 到顶后向右进入淋浴头范围：2m 包含在允许范围内，自动变为液态。转换不重置 XY 和高度。
4. 验证高度门槛：可在 Play 前暂把玩家放到 Shower 的地面 XY，再 Play。即使一开始已在范围内，也必须等 Height 达到 1.8m 才转换，无须退出再进入。
5. 验证最高交互高度：可在 Play 前暂把 Shower.maximumHeight 改为 1.9，等待玩家升到 2m 后再进入范围，此时不得转换。验收后恢复为 2，避免正常流程在天花板错过目标。

## 自动检查

退出 Play 后使用菜单 Emergence/Checks/Height。检查初期阻挡、升高后越过、持续上浮封顶、W/S 与高度解耦、交互上下限、范围内升高触发、气转液与下落承托、主体封顶与影子移动、转换时显示连续。

关联回归入口：Emergence/Checks/Interaction、Player Movement、Obstacle Destruction、Converter、Respawn、Camera、Player Prefab。

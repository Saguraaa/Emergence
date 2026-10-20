# 摄像机跟随

CameraController 在 LateUpdate 读取 PlayerPositionComponent，将 (XY.x, min(XY.y + Height * heightDisplayScale, maximumDisplayY)) 交给 CameraView。
CameraBootstrap 初始化时校验唯一 PlayerView，并读取它的 heightDisplayScale 和 maximumDisplayY；摄像机与主体使用同一高度显示比例和上限。
CameraBootstrap.heightFollowRatio 决定跟随点在地面 XY 和主体显示位置之间的比例，默认 1；Tutorial01 使用 0.5，并将正交大小设为 3.5，使上升过程可见且保留地面影子。
CameraView 负责平滑、偏移和视口安全区域，固定摄像机 Z，不跟随精灵缩放、粒子或重生动画。
出生及 RespawnSystem.Respawned 时立即定位。安全区域限制的是玩家跟随点，不是所有粒子覆盖的范围。

CameraBootstrap 校验并装配 Controller 与 View。GameBootstrap 在 Player 初始化后启动摄像机，
关闭时先停摄像机，再关闭 Player。未配置摄像机门控的旧场景保持现有行为。

Inspector：CameraView.targetCamera 指向正交摄像机，世界旋转为零；offset 设置构图偏移，
smoothTime 设置平滑时间；viewportMargin 默认 0.2，即玩家跟随点保持在视口横纵 20%～80% 内。
CameraController 在场景中默认禁用，由 CameraBootstrap.Initialize 开启，不自行启动。

## CameraTest 验收

场景：Assets/_Game/Scenes/Test/CameraTest.unity，打开后直接 Play。

- WASD 或方向键移动，靠近转换点按 E，沿用 Inspector 绑定的现有 InputAction。
- 转换点位于 y=-2，从左到右：x=-6 固→液、x=-2 液→气、x=2 气→液、x=6 液→固。
- 地面网格每格 2 单位，围墙内范围 x=-20～20、y=-14～14，范围大于相机视野。
- 四面墙均使用 BoxCollider2D，并经 ObstacleBootstrap 注册至统一 PhysicsSystem。
  墙体不可破坏，高度设为 1000，便于在持续升高的气态下验证横向阻挡。
- 在三种形态下移动、贴墙和沿墙移动，观察镜头平滑与阻挡。
- 气态停留会随风持续升高；镜头应跟随 XY.y + Height。此时地面会逐渐离开画面，属于预期表现。
  范围交互仍基于地面投影，可向 x=2、地面 y=-2 的气→液转换点移动并按 E，验证下降时的跟随。
- 如需直接从气态验收，可在 Play 前将 PlayerBootstrap.initialForm 设为 Gas。

自动检查菜单：Emergence/Checks/Camera。
覆盖出生定位、平滑、多种宽高比下的高速位移安全区域、固定 Z、持续气态升高、重生定位和关闭。

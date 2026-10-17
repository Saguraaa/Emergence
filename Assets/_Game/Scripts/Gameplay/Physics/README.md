# 统一物理系统

每个游戏 World 使用框架自动创建的一个 PhysicsSystem，不由 Bootstrap 重复创建。
碰撞形状、尺寸和偏移以 Unity Collider 为准，通过 Inspector 修改；ECS 和 Config 不保存副本。
ECS 保存权威位置、Velocity、当前形态和玩法状态。

PlayerSystem 计算期望位移，调用 PhysicsSystem.Move，随后使用允许位移及修正后的 Velocity 更新 ECS。
PhysicsSystem 统一使用 Collider2D.Cast、OverlapBox 和 Distance，处理阻挡、沿边滑动及出生或切换形态时的穿透。
只在 PhysicsSystem 中进行几何碰撞查询，复用查询结果集合，追加查询限制为四轮。
接触判断采用 Unity 的接触边距和小量距离容差，不要求两个可视外框严格重合。

PhysicsSystem 在固定步收集 PhysicsContactComponent 缓冲区，提供接触实体及法线。
ObstacleSystem 随后读取接触结果、InteractPressed、Solid 形态及可破坏 Tag，决定是否破坏。
破坏时立即停用注册的 Unity Collider，ObstacleController 负责读取破坏状态并刷新 ObstacleView 的精灵。

## Inspector 配置

玩家根物体：Kinematic Rigidbody2D，保持 simulated，禁用自主速度和旋转。
根物体下的 SolidCollider、LiquidCollider、GasCollider 独立于表现节点，分别配置 BoxCollider2D、CircleCollider2D、CircleCollider2D。
在 PlayerBootstrap 中绑定 Rigidbody2D、三个 Collider 和 collisionLayers；运行时仅启用当前形态的 Collider。
障碍物在 ObstacleBootstrap 中绑定 Collider2D，由门控注册到同一个 PhysicsSystem。
ObstacleView 的 SpriteRenderer 可空；未配置时只注册碰撞，不启用 ObstacleController 的逐帧显示刷新。
配置精灵时仍由 Controller 读取 ECS 破坏状态并刷新 View，不在逐帧方法里判空。
`obstacleHeight` 表示从地面起的障碍高度（默认 1），初始化写入 ObstacleHeightComponent。
玩家 Height 小于或等于障碍高度时阻挡，超过顶部时通过；所有形态使用同一规则。
Height 为独立游戏高度，不从精灵位置、Collider2D 尺寸或 Transform.z 推导。
EnvironmentBootstrap.maximumHeight 配置俯视场景的最高高度，初始化写入 EnvironmentComponent；默认 float.MaxValue 保留旧场景行为。
PlayerSystem 把该上限连同期望高度位移交给 PhysicsSystem.Move，物理系统将高度约束在 0～maximumHeight，并清除受阻的垂直 Velocity。
气态的 Height 只由 WindVelocity.z 驱动，W/S 只改变地面 XY；到达上限后仍可移动地面投影。侧视模式继续使用地图 Collider 限制顶部。
每步先在起点约束高度，再使用允许高度处理 XY 移动、穿透修正和接触收集。
下降时若地面投影与障碍重叠，则停在顶部上方的小量容差处，垂直 Velocity 清零；横向移出后继续下降。
这仅限制下降，不引入站立或平台状态。每次下降最多增加一次附近查询，复用原有集合。
复活使用出生点高度查询，切形态继续保留权威高度。飞越时不全局关闭障碍 Collider。
Actor 和 Obstacle Layer 已配置；参与查询的 Collider 必须由所属门控注册，保证结果可以对应 ECS 实体。

关闭对象时先停止 Controller，解除物理注册，再销毁 ECS 实体；不在逐帧方法里判空。
新增可移动对象也必须通过统一入口，不同时启用动态 Rigidbody 自主移动或独立几何判定。

## PipeMap 管壁（第二阶段）

`PipeCollision` 独立于显示用 Tilemap，共 6 段 BoxCollider2D：四条竖墙、横管上下两条墙；上方两个入口开放。
尺寸、位置、旋转直接在对应 Collider 的 Inspector 中调整，内部通道不铺设整块碰撞。
转角采用直角，两条主墙的端点略有重叠以覆盖接缝，不额外配置弯头碰撞。装饰性的管接头不封堵内部通道。

每段墙复用 ObstacleBootstrap，绑定 Collider2D、ObstacleController 和 ObstacleView，并加入 GameBootstrap 的 obstacleBootstraps。
管壁使用 Obstacle Layer，isDestructible 不勾选，ObstacleView 不配置 SpriteRenderer；显示继续由 Tilemap 负责。
门控由 GameBootstrap 在玩家之前 Initialize，向同一个 PhysicsSystem.RegisterObstacle 注册碰撞实体。
管壁沿用 ObstacleHeightComponent 的高度规则；独立 Height 不超过障碍高度时阻挡，超过时放行。
关闭时先停止移动对象及 Controller，再解除物理注册、关闭 Collider、销毁实体。

管壁检查覆盖两侧阻挡、底部阻挡、横管和直角连通；侧视移动规则如下。

## 侧视移动（第三阶段）

EnvironmentBootstrap 的 Movement Mode 默认为 TopDown，现有俯视场景保持原来的 XY 输入与独立 Height 规则。
PipeMap 选择 SideView，Gravity 为 9.81，Wind Velocity 为 `(0, 1, 0)`，玩家初始 Height 为 0。
模式初始化写入 EnvironmentComponent，运行规则由 PlayerSystem 执行。

SideView 只读取左右输入，忽略上下输入且不因此降低水平移动量。固态保留水平加减速，液态直接响应水平输入。
键盘 Move 复合输入使用 Digital 模式保留原始轴值；TopDown 仍由 PlayerSystem 统一归一化斜向输入，手柄保留模拟量。
固态、液态在 Velocity.y 中累计重力，用匀加速位移积分，不再被每步水平输入覆盖；气态由 WindVelocity.xy 驱动。
气态转固态或液态时取消上浮，已有下落速度继续继承。侧视模式的 Height 与 Velocity.z 保持 0。
所有期望位移仍经过同一个 PhysicsSystem.Move；落地清除向下速度，碰侧壁后继续下落，气态受到顶部阻挡。
接触约束复用已有距离查询，并包含 Unity 的接触边距，同时消除朝向表面的位移与速度，避免静止后再次积累下落。
不使用动态 Rigidbody 自主重力，不在 View 或 Controller 中计算下落。

代码检查入口：`Emergence/Checks/Player Movement`，同时覆盖俯视回归、侧视积分、输入、形态转换和落地/碰壁/碰顶。

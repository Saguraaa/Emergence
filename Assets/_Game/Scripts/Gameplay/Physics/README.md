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
障碍物在 ObstacleBootstrap 中绑定 Collider，由门控注册到同一个 PhysicsSystem。
`obstacleHeight` 表示从地面起的障碍高度（默认 1），初始化写入 ObstacleHeightComponent。
玩家 Height 小于或等于障碍高度时阻挡，超过顶部时通过；所有形态使用同一规则。
Height 为独立游戏高度，不从精灵位置、Collider2D 尺寸或 Transform.z 推导。
每步先在起点约束高度，再使用允许高度处理 XY 移动、穿透修正和接触收集。
下降时若地面投影与障碍重叠，则停在顶部上方的小量容差处，垂直 Velocity 清零；横向移出后继续下降。
这仅限制下降，不引入站立或平台状态。每次下降最多增加一次附近查询，复用原有集合。
复活使用出生点高度查询，切形态继续保留权威高度。飞越时不全局关闭障碍 Collider。
Actor 和 Obstacle Layer 已配置；参与查询的 Collider 必须由所属门控注册，保证结果可以对应 ECS 实体。

关闭对象时先停止 Controller，解除物理注册，再销毁 ECS 实体；不在逐帧方法里判空。
新增可移动对象也必须通过统一入口，不同时启用动态 Rigidbody 自主移动或独立几何判定。

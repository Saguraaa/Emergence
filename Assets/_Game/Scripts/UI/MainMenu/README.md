# 开始界面 · 三阶段

打开 `Assets/_Game/Scenes/UI/MainMenu.unity`，进入 Play 模式即可验收。

- 使用 UGUI 的 Canvas、Image、Button，直接引用 `Art/UI` 原有背景和四个按钮切片；按钮文字已包含在素材中。
- 1280 × 720 为设计基准，保持 16:9 完整构图，其他比例居中留黑边。
- 左侧依次为新游戏、继续游戏、设置，右上角为关于我们；支持鼠标点击与 UI 导航。
- 选择 `MainMenuCanvas/Artwork` 上的 `MainMenuPanel`，可在 Inspector 为四个 `Requested` UnityEvent 绑定响应；代码也可通过对应公共属性订阅。
- 设置事件由场景总入口连接到设置面板；新游戏和继续游戏已接入关卡自动存档，四个 UnityEvent 仍可用于附加响应。关于事件暂不绑定业务逻辑。
- `MainMenuSceneBootstrap` 只负责装配和清理，不创建玩法实体，不加载关卡，不写存档。

第二阶段设置详见 `UI/Settings/README.md`。第三阶段存档详见 `Gameplay/Save/README.md`；无自动存档时继续游戏禁用，新游戏重建存档，继续游戏从存档关卡的复活点开始。

# Emergence 项目结构

项目自有代码与资源放在 `_Game` 下，第三方资源保留原目录。

```text
_Game/
├─ Scripts/
│  ├─ Bootstrap/            游戏入口：初始化、连接依赖、清理资源
│  ├─ Gameplay/             ECS 数据与玩法规则
│  │  └─ Interaction/       拾取、交互对象与交互规则
│  ├─ UI/                   界面 View 与 Controller
│  │  ├─ HUD/               游戏内状态与提示
│  │  └─ PauseMenu/         暂停界面
│  └─ WorldView/            场景对象的视觉表现
├─ Scenes/                 游戏场景
├─ Prefabs/
│  ├─ Gameplay/             玩法对象预制体
│  └─ UI/                   界面预制体
├─ Data/                    物品、谜题、关卡等配置资源
├─ Art/                     图片、动画、材质等美术资源
├─ Audio/                   音乐与音效
└─ Settings/                游戏自有设置
```

## 代码约定

- ECS 的组件与系统共同承担 MVC 的 Model 职责；游戏运行状态由 ECS 持有。
- Controller 直接连接 ECS，提交操作请求、读取状态，再调用 View 刷新界面。
- 玩法判断放在 System 中；当前选中的格子等简单界面状态可以放在 Controller 中。
- View 负责显示与发出界面操作事件；WorldView 负责场景中的动画、精灵等表现。
- Bootstrap 负责装配与生命周期。若 ECS 框架已有自动初始化，不重复创建 World 或注册系统。
- 简单界面可以用一个 Panel 脚本同时承担 View 与 Controller 职责。
- 文件较少时直接放在功能目录下；按需要再增加 Components、Systems 等子目录。
- ScriptableObject 配置资源放在 Data 中，相关 C# 类型放在所属玩法目录中。

现有 URP 与输入配置保留在 `Assets/Settings`。新场景保存到 `_Game/Scenes`，
创建后按需要加入构建场景列表。

空目录内的 `.gitkeep` 用于让 Git 保存目录，添加实际文件后可以删除。

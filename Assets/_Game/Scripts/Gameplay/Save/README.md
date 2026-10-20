# 关卡自动存档 · 第三阶段

从 `Assets/_Game/Scenes/UI/MainMenu.unity` 进入游戏。

- 使用一个自动存档：`Application.persistentDataPath/Saves/progress.json`。当前工程在 Windows 编辑器中的位置为 `C:/Users/saguraaa/AppData/LocalLow/DefaultCompany/Emergence/Saves/progress.json`。
- 没有存档目录或自动存档文件时，「继续游戏」禁用；文件中的关卡未列入启用的构建场景时也不能继续。
- 点击「新游戏」立即创建或重建自动存档，记录教程关卡，随后加载教程。首关由 MainMenuPanel 的 `First Level Scene` Inspector 引用配置。
- JSON 仅保存 `level` 场景路径，不保存位置、形态、输入、障碍物或其他玩家状态。
- 正常进入另一关卡并完成所有装配后，更新存档为该关卡。返回旧关卡也会更新为当前关卡。
- 点击「继续游戏」加载存档关卡，由该关卡启用的 ECS 复活点提供 XY 和 Height。每个可继续的关卡必须恰好有一个启用的复活点。
- 新游戏沿用场景原本的初始出生位置；继续游戏使用复活点。教程使用原有 `RespawnPoint_Toilet`；管道关卡在原玩家出生位置添加 `RespawnPoint_Pipe`，复用已有 RespawnPoint prefab。
- `LevelProgressComponent` 保存当前关卡和加载请求状态；`LevelProgressSystem` 执行存档、加载请求和出生位置规则。MainMenuPanel 提交请求、更新按钮并调用 Unity 场景加载。
- PlayerBootstrap 在物理注册前调用 System 应用继续游戏的位置；GameBootstrap 完成关卡装配后通知 System 更新进度。场景门控继续只负责初始化、装配和清理，未增加架构层。
- 直接在编辑器运行玩法/测试场景，不会创建或覆盖自动存档；从菜单开始的游戏会自动保存。
- 加载请求期间禁止重复新游戏、继续游戏，避免重复建档和场景加载。

## 检查

菜单 `Emergence/Checks/Save` 检查空存档、新建、只保存关卡、重复请求、跨关卡保存、复活点位置、新游戏重置和不可加载的关卡。检查结束会恢复原始存档内容；原本没有存档时会移除测试产生的文件和空目录。

Play 模式验收：无存档时继续按钮禁用 → 新游戏生成教程存档 → 传送到管道关卡后存档更新 → 重新运行菜单后继续游戏 → 出现在管道复活点。随后继续检查教程复活点及原有传送、死亡重生。

已通过 19 项存档检查、14 项原有复活检查、22 项原有传送检查，以及上述 Play 完整流程。继续游戏时 ECS、Rigidbody2D 和 View 同步至管道复活点 (-5.5, 16) 或教程复活点 (-5, -1)。新游戏仍使用教程初始出生位置 (1.97, -1.83)。测试存档已清理，原有存档状态已恢复。

菜单总入口订阅新游戏和继续游戏事件，在场景加载前主动关闭界面与输入，避免卸载时访问已经销毁的设置面板。

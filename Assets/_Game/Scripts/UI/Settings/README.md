# 设置面板 · 第二阶段

在 `Assets/_Game/Scenes/UI/MainMenu.unity` 进入 Play，点击「设置」。

- `TabContainer` 负责显示、声音、操作三个 UGUI 页签，每次仅显示一个页面。
- 显示：枚举当前设备支持的分辨率，支持无边框全屏、窗口、独占全屏；帧率提供 30、60、120、144 和不限。开启垂直同步时禁用手动帧率选择，关闭后恢复原选择。Unity 编辑器中的 Game View 不会改变操作系统分辨率；窗口和分辨率切换应在桌面构建中验收。
- 声音：背景音乐和音效分别控制 `Assets/_Game/Audio/GameAudio.mixer` 的 Music、SoundEffects 通道，0% 为 -80 dB 静音。背景音乐素材位于 `Assets/_Game/Audio/Music/BackgroundMusic.wav`，原文件为以 MP3 扩展名保存的 QuickTime 音频，已转换为 WAV；Unity 使用 Vorbis 流式加载。`Assets/_Game/Prefabs/Audio/BackgroundMusic.prefab` 已放入 MainMenu、Tutorial01 和 PipeMap，使用 Music 通道、2D 自动循环播放，基础音量为 30%；切换场景时从头播放。添加其他 AudioSource 时，将 Output 连接到对应通道。
- 操作：支持 WASD、方向键、E、左右 Alt，共 11 个键盘绑定入口。复用 `InputSettingsController`，绑定引用来自 Inspector，按 Action ID 解析运行时副本。开始菜单的 UI 输入也使用同一副本。改键期间仅暂停所属玩法 Action Map，菜单仍能响应取消、切页和返回；Esc 也可取消。键盘绑定不会误录为鼠标或手柄输入。
- 「恢复默认按键」清除已有输入资源的全部改键记录，沿用原来的 `Emergence.Player.Bindings.v1` 存储键。显示和声音设置使用 `Emergence.Settings.*` 偏好键，修改后立即保存。
- `SettingsPanel` 合并界面与 Controller，只处理界面、Unity 显示/音频设置和输入绑定，不重复保存 ECS 玩法状态。`MainMenuSceneBootstrap` 只装配界面和输入，并按相反顺序清理。
- 场景中的文字、颜色、尺寸、控件和绑定均可在 Inspector 编辑；中文字体随项目提供，授权文件位于 `Art/UI/Fonts`。

已完成运行验证：页签互斥；帧率和垂直同步；独立音量及静音；真实模拟键盘输入改键；保存与重新载入；Esc/取消/切页/返回取消改键；恢复默认；原始输入资源不变。

第三阶段关卡自动存档已接入，详见 `Gameplay/Save/README.md`。

关卡内设置：`Tutorial01` 和 `PipeMap` 使用 `Assets/_Game/Prefabs/UI/Settings.prefab`，由 `GameBootstrap` 调用 `SettingsBootstrap.Initialize/Shutdown` 装配和释放。面板的 Inspector 绑定 UI/Cancel 动作，按 Esc 打开或关闭；开始界面不绑定该快捷键，仍点击「设置」进入。UI 与玩家共用 `InputSettingsController` 的运行时输入副本。

关卡打开设置时屏蔽玩家操作，关闭后恢复原先的输入状态；不暂停物理和关卡时间。展开下拉框时 Esc 先收起列表，改键时 Esc 只取消改键。两处设置面板均提供「退出游戏」：桌面构建退出程序，Unity 编辑器中停止 Play。

鼠标在进入关卡时隐藏，打开设置时显示，关闭设置后重新隐藏；开始界面和关闭界面模块后显示鼠标。由 `SettingsPanel` 在初始化、打开、关闭和清理时设置 `Cursor.visible`，无需逐帧更新。

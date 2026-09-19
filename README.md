# 叠叠裙

竖屏整理类小游戏。棋盘上是一列列叠着的裙子，点一列就把手里那件插到该列最上面、
把该列最下面那件顶出来到手里。全部按款式归类好、手里只剩那个问号礼盒时过关，
礼盒翻开解锁一件新裙子，进衣柜给角色换装。

目标平台是微信小游戏，引擎是团结引擎 2022.3.62t8（Unity 2022.3 内核）。

## 跑起来

在编辑器里依次点：

1. `叠叠裙 / 重建资源与场景` — 把 `Art/` 下的图打成 ScriptableObject 并生成场景
2. `叠叠裙 / 打开场景` — 打开后直接 Play
3. Game 窗口会自动切到 **微信竖屏 720×1280**（和 black-rosa / 微信开发者工具一致）

命令行自检（会跑完玩法验证并输出五个页面的截图到 `Screenshots/`）：

```bash
Tuanjie -batchmode -projectPath . \
    -executeMethod DressSort.EditorTools.DressSortVerify.Run -quit
```

编辑器开着同一个工程时 batchmode 会被文件锁挡住，把工程复制一份到别处跑即可。

## 代码结构

```
Assets/DressSort/
  Scripts/
    Data/     ItemDef  LevelDef  ChapterDef  GameDatabase     配置，纯数据
    Core/     SortBoard                                       玩法规则，不依赖 Unity
              WardrobeService                                 存档
    View/     UiKit  BoardView  PaperDoll                     渲染
    Screens/  App  HomePanel  LevelMapPanel  GamePanel
              RewardPanel  DressUpPanel                       页面
  Editor/     DressSortBuilder  DressSortVerify  SpriteImportSettings
  Art/        Icons  Portraits  Wings                         管线产出的贴图
  Data/       Items  Levels  Chapter01  GameDatabase          Builder 生成，不要手改
```

几个刻意的选择：

**玩法规则和渲染完全分开。** `SortBoard` 是纯 C#，一行 UnityEngine 都没有，
所以自检脚本能直接跑几百局求解来确认每一关在步数上限内有解。

**只有一个场景。** `App` 建一个 Canvas，五个页面是同一个 Canvas 下的面板，
靠显示隐藏切换，没有场景加载，微信小游戏上切页面不会卡。

**界面控件是程序化生成的。** 圆角底图是运行时画出来的九宫格贴图（`UiKit`），
工程里不需要一堆 UI 切图，改圆角半径只要改一个数。

**棋盘尺寸和款式从配置读。** 换主题只要新建 `LevelDef`，不用碰玩法代码。

## 关卡与内容

第一章「甜心裙」八关，五列，列高从 5 递增到 6，打乱步数 16 到 44。
十件物品：八条裙子 + 两对翅膀，初始送洋红泡泡袖裙和柠檬黄背带裙，
其余八件按关卡奖励依次解锁。

关卡从"已解开"的状态倒着走若干步生成，所以一定有解；自检里的求解器确认
八关分别在 16 到 49 步内能解完，都在步数上限之内。

## 美术管线

见 [docs/art-pipeline.md](docs/art-pipeline.md)。要点：AI 只画近黑紫轮廓不画白边，
白边由代码补；立绘是身体和裙子烘在一起的单张图；同批图标强制等高。

## 微信小游戏

做法对齐 [black-rosa](../black-rosa/README.md)。团结规定：微信子平台没 Active 时，转换 SDK 不编译，顶栏也就没有「微信小游戏」。

1. `叠叠裙 / 微信小游戏 / 接入 SDK` — 从隔壁 black-rosa 拷 `com.qq.weixin.minigame`（133MB，不入库）
2. 等脚本编译完，顶栏出现 **微信小游戏**
3. `叠叠裙 / 微信小游戏 / 打开转换面板`，或顶栏 **微信小游戏 → 转换小游戏 → 生成并转换**
4. 导出路径是仓库里的 `wechat-minigame`，首包选「小游戏包内」
5. 用微信开发者工具打开 **`game2D_cat/wechat-minigame/minigame/`**，不要打开仓库根目录，也**不要打开 `black-rosa/wechat-minigame/minigame`**。两个窗口一起开着时，上传很容易传到墨字防线。

也可以 `叠叠裙 / 微信小游戏 / 生成并转换` 直接跑。第一次编 WASM 可能要十几分钟。

AppID 必须是叠叠裙的 `wxd7e8bab5c9f43673`。SDK 从 black-rosa 拷过来时自带墨字防线 `wxad27f529c95e582c`，接入时会改掉。开发者工具详情里如果还写着墨字防线，先关掉那个项目，重新打开本仓库的 `wechat-minigame/minigame/`。

开发者工具若提示快适配未开通：用正式 AppID 登录微信公众平台，在「能力地图 → 开发提效包 → 快适配」开通。测试号不能用。

预览分辨率是 **720×1280 竖屏**。画布设计稿仍是 1080×1920（9:16），按宽匹配，刘海屏多出来的高度让给胶囊安全区。

## 还没做的

- 移出槽（把手里那件暂存到旁边）
- 音效与背景音乐
- Addressables 按章节分包、首包瘦身
- 换发型用的头部分层

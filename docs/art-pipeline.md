# 叠叠裙美术管线

从提示词到 Unity 里可用的 Sprite，全程只有两条命令。

- 提示词：`docs/prompt/`
- **生图母版必须写到绝对路径**（确认前不要拷进游戏仓）：
  `/Users/rosa/rosa_games/game_assets/game2D_cat/assets/raw/dresssort/<任务名>/`
- 现成任务目录：`branding/`（loading / 头像 / logo）、`cast-hairdress/`（前发后发三连图）、`cast-pick/`（选角）、`bg-v8/`（页面背景）
- **禁止**把 Gemini 母版写进仓库内 `game_assets_tmp/`。那个目录只给本机脚本的临时中间文件，Finder / 聊天里都难找。
- 成品：`Assets/DressSort/Art/`

```bash
# 粉发水手裙这一版
/usr/bin/python3 Tools/artpipe.py v7

# 3. 打成 ScriptableObject 并建场景（Unity 菜单）
叠叠裙 / 重建资源与场景
```

v7 原图在仓库外 `../game_assets/game2D_cat/assets/raw/dresssort/v7/`。
界面底板、按钮、列框是 `Art/Ui/` 里的切图，不要再代码画圆角色块。

## 风格基准

| 项 | 取值 |
|---|---|
| 画法 | 日式电视动画赛璐璐，细匀黑线 |
| 配色 | 牛奶底 `#F7FBFC` / 泳池蓝 `#2A9B96` / 水蜜桃粉 `#F3A7B8` / 香草卡片 `#FFF6EC` |
| 角色 | 以 `Art/Cast/fullbody/full_apricot.png` 为脸和比例基准：杏橙发、茶褐椭圆眼、得意浅笑 |
| 棋盘裙标 | 锁定无袖色块，见 `docs/prompt/board-dress-spec.txt`，基准 `Art/Icons/dress_black_ribbon.png` |
| 生图背景 | 全身像用纯白底去底；控件和裙子表用 `#4A5A6B` 灰蓝去底；页面底图直接铺满 |

## 换装切层（现行）

新发型 / 新裙子一起出一张全身像。发型整团待在头上、发梢停在衣领以上，用户切成头 + 身体两张，再用 `python3 Tools/sprite_align.py` 对齐到基准画布。游戏里同尺寸透明底直接叠。披肩发要前后分片，先不出。参考图只用基准脸部特写，不要贴整身水手裙。

## 三条关键约定

**白边一律由代码加，AI 不画。** 白边宽度 AI 控不住，而且立绘是分层叠的，
每层自带白边会在接缝处露出白豁口。`artpipe.py` 的 `white_ring()` 用形态学膨胀
统一补，只给需要贴纸感的地方补：棋盘上的裙子、衣柜格子、礼盒、星星。
角色立绘和翅膀不补。

**立绘是"身体 + 裙子"烘在一起的单张图。** 试过分件生成裙子再运行时对位，
AI 把百分比定位理解成相对画布而不是相对人偶，缩放和偏移都对不上，
腰线自动对齐也因为手部像素干扰而失败。改成直接生成"人偶穿着这条裙子"，
以基准立绘当参考图，头部位置在八张里误差在 1px 内。

**同一批图标必须等高。** `fit_height()` 把每件缩放到统一的内容高度再放到统一画布，
所以一列叠六件不会被最高的那件撑爆。做这步之前先 `despeckle()`——生图常在
轮廓外甩出几个小白点，它们会把内容包围盒撑大，导致整件被缩小。

## 分层顺序

游戏里所有换装都按这个顺序叠：

```
翅膀 → 后发 hairback → 衣服/身体 → 头和前发
```

有 `Portraits/body_{id}.png` 的裙子是无头身体，换发时叠头。还没拆头的用 `Portraits/portrait_{id}.png` 兜底。

## 文件产出

对齐工具的长文件名只留在 `Art/Cast/`。游戏内资源一律用短 id，和 `DressSortBuilder` 里的中文名对应。

| 文件 | 内容 |
|---|---|
| `Art/Icons/dress_{id}.png` | 棋盘与衣柜裙子图标 |
| `Art/Icons/hair_{key}.png` | 衣柜发型图标 |
| `Art/Portraits/body_{id}.png` | 无头身体，可换发 |
| `Art/Portraits/portrait_{id}.png` | 未拆头的全身兜底 |
| `Art/Portraits/head_{key}.png` | 头和前发 |
| `Art/Portraits/hairback_{key}.png` | 长发后片，夹在衣服后面 |
| `Art/Wings/{id}.png` | 翅膀 |

裙子 id：`teal_sailor` 泳池水手裙、`black_ribbon` 黑裙白结、`pink_gingham` 粉格蛋糕裙、`lemon_print` 柠檬衬衫裙、`orange_slice` 橙子背心裙、`ivory_lace` 象牙蕾丝裙、`strawberry` 草莓开衫裙、`grape_school` 葡萄校服。
发型 id：`hair_apricot` 杏橙、`hair_milktea` 奶茶棕、`hair_milktea_long` 奶茶长发、`hair_wisteria` 紫藤、`hair_caramel` 焦糖栗、`hair_baguette` 奶油金、`hair_denim` 雾蓝。

第一章全部资源共 1.3 MB（30 个 PNG，已过 pngquant），首包 4MB 的预算够用。

## 中文字体

`Tools/subset_font.py` 扫 `Assets/DressSort/Scripts` 和 `Editor` 里的字符串字面量，
从 Noto Sans SC 可变字体裁出只含用到字形的子集，输出
`Assets/DressSort/Resources/Fonts/UiFont.ttf`（131 KB，229 个中日韩字形）。

**界面上加了新文案，必须重跑一次这个脚本**，否则新字会显示成方框。
字体是 SIL Open Font License 1.1，可随游戏分发。

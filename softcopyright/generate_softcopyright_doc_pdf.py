#!/usr/bin/env python3
"""
软件著作权登记 - 文档鉴别材料（设计说明书）PDF 生成工具
项目: 一裙又一裙小游戏软件

说明:
  正文按一裙又一裙的整理玩法、关卡机制、换装、工坊和云端排行编写。
  截图未放入 softcopyright/pics 时，PDF 中自动生成「待补游戏截图」占位框。
"""

import warnings
from pathlib import Path

from fpdf import FPDF
from fpdf.enums import WrapMode
from PIL import Image

warnings.filterwarnings("ignore", category=DeprecationWarning)


SCRIPT_DIR = Path(__file__).resolve().parent
PROJECT_ROOT = Path('/Users/huyi/dk_proj/game2D_cat')
OUTPUT = SCRIPT_DIR / '软著文档-一裙又一裙-V1.0.0.pdf'

SOFTWARE_FULL_NAME = '深圳幸运呱科技有限公司一裙又一裙小游戏软件'
SOFTWARE_VERSION = 'V1.0.0'
APPLICANT_NAME = '深圳幸运呱科技有限公司'

SONGTI_PATH = '/System/Library/Fonts/Supplemental/Songti.ttc'

BODY_FONT_SIZE = 10.5
H1_FONT_SIZE = 16
H2_FONT_SIZE = 14
H3_FONT_SIZE = 12
CODE_FONT_SIZE = 9
HEADER_FONT_SIZE = 10
FOOTER_FONT_SIZE = 9

LINE_HEIGHT = 6.5
CODE_LINE_HEIGHT = 5.0
H1_LINE_HEIGHT = 10
H2_LINE_HEIGHT = 8.5
H3_LINE_HEIGHT = 7.5

LEFT_MARGIN = 25
RIGHT_MARGIN = 20
TOP_MARGIN = 15
BOTTOM_MARGIN = 15

PAGE_W = 210
PAGE_H = 297
CONTENT_W = PAGE_W - LEFT_MARGIN - RIGHT_MARGIN
CONTENT_TOP = TOP_MARGIN + 10

HEADER_TEXT = f'{SOFTWARE_FULL_NAME} {SOFTWARE_VERSION} 设计说明书'
PICS_DIR = SCRIPT_DIR / 'pics'
IMAGE_EXTS = {'.jpg', '.jpeg', '.png'}


def _pic(stem):
    """按主文件名找截图。jpg/png 都认，也认 dress_01_home 这种带前缀的名字。"""
    fallback = PICS_DIR / f'{stem}.jpg'
    if not PICS_DIR.exists():
        return fallback
    exact = []
    prefixed = []
    for path in PICS_DIR.iterdir():
        if not path.is_file() or path.suffix.lower() not in IMAGE_EXTS:
            continue
        if path.stem == stem:
            exact.append(path)
        elif path.stem.endswith('_' + stem):
            prefixed.append(path)
    if exact:
        return sorted(exact)[0]
    if prefixed:
        return sorted(prefixed)[0]
    return fallback


SCREENSHOTS = {
    'loading': [(_pic('00_loading'), '启动页  加载界面 - 游戏名称、进度与健康游戏忠告')],
    'home': [(_pic('01_home'), '图1  首页 - 立绘、体力、开始关卡与侧边入口')],
    'game': [(_pic('02_game'), '图2  整理关卡 - 衣架列、手里的裙子与步数')],
    'parcel': [(_pic('03_parcel'), '图3  包裹 - 盖住的格子落到列底才拆开')],
    'cover': [(_pic('04_cover'), '图4  防尘罩 - 叠好指定列数后拉开')],
    'target': [(_pic('05_target'), '图5  专属列 - 列顶气泡指定只能叠的款式')],
    'lock': [(_pic('06_lock'), '图6  锁和钥匙 - 顶出带钥匙的裙子打开上锁的列')],
    'alarm': [(_pic('07_alarm'), '图7  限时闹钟 - 倒计时归零前要把这件顶出来')],
    'reward': [(_pic('08_reward'), '图8  过关领奖 - 礼盒揭开后的裙子或图纸')],
    'dressup': [(_pic('09_dressup'), '图9-1  试衣间 - 裙子页签与已解锁的衣服')],
    'hair': [(_pic('09_dressup_hair'), '图9-2  试衣间换发型 - 发型页签与已选头发')],
    'checkin': [(_pic('10_checkin'), '图10  七日签到 - 每天体力与第 7 天大份')],
    'quest': [(_pic('11_quest'), '图11  过关任务 - 按通关进度领取衣服、材料或图纸')],
    'workshop': [(_pic('12_workshop'), '图12  工坊 - 图纸、材料数量与制作')],
    'pack': [(_pic('13_pack'), '图13  同款装箱 - 顶上同款倒列、收纳箱与进度')],
    'energy': [(_pic('14_energy'), '图14  体力不足 - 观看激励视频恢复 1 点体力')],
    'rank': [(_pic('15_rank'), '图15  通关排行榜 - 按已通关关数排序，自己钉在底部')],
}


class DocPDF(FPDF):
    def __init__(self):
        super().__init__(orientation='P', unit='mm', format='A4')
        self.set_left_margin(LEFT_MARGIN)
        self.set_right_margin(RIGHT_MARGIN)
        self.set_top_margin(CONTENT_TOP)
        self.set_auto_page_break(auto=True, margin=BOTTOM_MARGIN + 10)
        self.missing_images = []

    def header(self):
        self.set_font('Songti', '', HEADER_FONT_SIZE)
        self.set_text_color(0, 0, 0)
        self.set_xy(LEFT_MARGIN, TOP_MARGIN)
        self.cell(0, 6, HEADER_TEXT, new_x='LEFT', new_y='TOP')
        page_str = str(self.page_no())
        tw = self.get_string_width(page_str)
        self.set_xy(PAGE_W - RIGHT_MARGIN - tw, TOP_MARGIN)
        self.cell(tw, 6, page_str, new_x='LEFT', new_y='TOP')
        line_y = TOP_MARGIN + 7
        self.set_draw_color(0, 0, 0)
        self.set_line_width(0.4)
        self.line(LEFT_MARGIN, line_y, PAGE_W - RIGHT_MARGIN, line_y)
        self.set_y(CONTENT_TOP)

    def footer(self):
        footer_y = PAGE_H - BOTTOM_MARGIN
        self.set_xy(LEFT_MARGIN, footer_y)
        self.set_font('Songti', '', FOOTER_FONT_SIZE)
        self.set_text_color(0, 0, 0)
        self.cell(CONTENT_W, 5, APPLICANT_NAME, align='C')

    def check_page_break(self, h):
        if self.get_y() + h > PAGE_H - BOTTOM_MARGIN - 10:
            self.add_page()

    def write_h1(self, text):
        self.check_page_break(H1_LINE_HEIGHT + 5)
        self.ln(4)
        self.set_font('Songti', '', H1_FONT_SIZE)
        self.set_text_color(0, 0, 0)
        self.set_x(LEFT_MARGIN)
        self.cell(CONTENT_W, H1_LINE_HEIGHT, _safe_text(text), new_x='LMARGIN', new_y='NEXT')
        self.ln(2)

    def write_h2(self, text):
        self.check_page_break(H2_LINE_HEIGHT + 4)
        self.ln(3)
        self.set_font('Songti', '', H2_FONT_SIZE)
        self.set_text_color(0, 0, 0)
        self.set_x(LEFT_MARGIN)
        self.cell(CONTENT_W, H2_LINE_HEIGHT, _safe_text(text), new_x='LMARGIN', new_y='NEXT')
        self.ln(1.5)

    def write_h3(self, text):
        self.check_page_break(H3_LINE_HEIGHT + 3)
        self.ln(2)
        self.set_font('Songti', '', H3_FONT_SIZE)
        self.set_text_color(0, 0, 0)
        self.set_x(LEFT_MARGIN)
        self.cell(CONTENT_W, H3_LINE_HEIGHT, _safe_text(text), new_x='LMARGIN', new_y='NEXT')
        self.ln(1)

    def write_body(self, text, indent=0):
        self.set_font('Songti', '', BODY_FONT_SIZE)
        self.set_text_color(30, 30, 30)
        self.set_x(LEFT_MARGIN + indent)
        self.multi_cell(CONTENT_W - indent, LINE_HEIGHT, _safe_text(text),
                        new_x='LMARGIN', new_y='NEXT', wrapmode=WrapMode.CHAR)

    def write_bullet(self, text, level=0):
        indent = 4 + level * 4
        bullet = '  ' * level + ('- ' if level > 0 else '* ')
        self.write_body(bullet + text, indent=indent)

    def write_code_block(self, lines):
        self.ln(1)
        self.set_font('Songti', '', CODE_FONT_SIZE)
        self.set_text_color(40, 40, 40)
        for line in lines:
            self.check_page_break(CODE_LINE_HEIGHT)
            self.set_fill_color(245, 245, 245)
            self.set_x(LEFT_MARGIN + 4)
            self.cell(CONTENT_W - 4, CODE_LINE_HEIGHT, _safe_text(line.replace('\t', '    ')),
                      fill=True, new_x='LMARGIN', new_y='NEXT')
        self.ln(1)

    def write_table(self, headers, rows, col_widths=None):
        self.ln(1)
        if col_widths is None:
            col_widths = [CONTENT_W / len(headers)] * len(headers)
        row_line_h = 5.6
        pad_x = 1.5
        pad_y = 1.5

        def wrap_cell(text, width):
            text = _safe_text(str(text))
            lines = []
            for paragraph in text.split('\n'):
                current = ''
                for ch in paragraph:
                    if self.get_string_width(current + ch) <= width:
                        current += ch
                    else:
                        if current:
                            lines.append(current)
                        current = ch
                lines.append(current)
            return lines or ['']

        def draw_row(cells, fill):
            self.set_font('Songti', '', BODY_FONT_SIZE)
            wrapped = [wrap_cell(c, col_widths[i] - pad_x * 2) for i, c in enumerate(cells)]
            row_h = max(len(lines) for lines in wrapped) * row_line_h + pad_y * 2
            self.check_page_break(row_h)
            y0 = self.get_y()
            x = LEFT_MARGIN
            self.set_fill_color(*fill)
            self.set_draw_color(0, 0, 0)
            for i, lines in enumerate(wrapped):
                self.rect(x, y0, col_widths[i], row_h, style='DF')
                self.set_xy(x + pad_x, y0 + pad_y)
                for line in lines:
                    self.cell(col_widths[i] - pad_x * 2, row_line_h, line,
                              new_x='LEFT', new_y='NEXT')
                    self.set_x(x + pad_x)
                x += col_widths[i]
            self.set_y(y0 + row_h)

        self.set_font('Songti', '', BODY_FONT_SIZE)
        self.set_text_color(0, 0, 0)
        draw_row(headers, (230, 230, 230))
        for row in rows:
            self.set_text_color(30, 30, 30)
            draw_row(row, (255, 255, 255))
        self.ln(1)

    def write_image(self, img_path, caption='', max_h=90):
        img_path = Path(img_path)
        if not img_path.exists():
            self._write_image_placeholder(img_path, caption)
            return

        img = Image.open(img_path)
        iw, ih = img.size
        max_w = CONTENT_W * 0.48
        ratio = min(max_w / iw, max_h / ih)
        draw_w = iw * ratio
        draw_h = ih * ratio
        total_h = draw_h + 18
        self.check_page_break(total_h)
        self.ln(3)
        x = LEFT_MARGIN + (CONTENT_W - draw_w) / 2
        self.image(str(img_path), x=x, y=self.get_y(), w=draw_w, h=draw_h)
        self.set_y(self.get_y() + draw_h + 2)
        if caption:
            self.set_font('Songti', '', 9)
            self.set_text_color(100, 100, 100)
            self.set_x(LEFT_MARGIN)
            self.cell(CONTENT_W, 5, _safe_text(caption), align='C', new_x='LMARGIN', new_y='NEXT')
            self.set_text_color(30, 30, 30)
        self.ln(3)

    def _write_image_placeholder(self, img_path, caption):
        self.missing_images.append((str(img_path), caption))
        box_h = 58
        self.check_page_break(box_h + 16)
        self.ln(3)
        x = LEFT_MARGIN + CONTENT_W * 0.22
        w = CONTENT_W * 0.56
        y = self.get_y()
        self.set_draw_color(120, 120, 120)
        self.set_fill_color(248, 248, 248)
        self.rect(x, y, w, box_h, style='DF')
        self.set_font('Songti', '', 11)
        self.set_text_color(120, 120, 120)
        self.set_xy(x, y + 18)
        self.cell(w, 7, '待补游戏截图', align='C', new_x='LEFT', new_y='NEXT')
        self.set_xy(x, y + 28)
        self.cell(w, 7, img_path.name, align='C', new_x='LEFT', new_y='NEXT')
        self.set_y(y + box_h + 2)
        self.set_font('Songti', '', 9)
        self.set_text_color(100, 100, 100)
        self.set_x(LEFT_MARGIN)
        self.cell(CONTENT_W, 5, _safe_text(caption + '（截图占位）'), align='C',
                  new_x='LMARGIN', new_y='NEXT')
        self.set_text_color(30, 30, 30)
        self.ln(3)


def _safe_text(text):
    replacements = {
        '→': '->', '←': '<-', '↑': '^', '↓': 'v', '★': '*',
        '“': '"', '”': '"', '‘': "'", '’': "'",
        '—': '-', '·': '.', '：': ':', '（': '(', '）': ')',
    }
    for old, new in replacements.items():
        text = text.replace(old, new)
    return ''.join(c if ord(c) <= 0xFFFF else '?' for c in text)


def img(key):
    return SCREENSHOTS[key][0]


def write_document(pdf):
    pdf.add_page()
    pdf.write_h1('目  录')
    for item in [
        '一、引言',
        '    1.1 编写目的',
        '    1.2 软件概述',
        '    1.3 运行环境',
        '    1.4 术语与缩略语',
        '二、软件总体设计',
        '    2.1 软件需求概括',
        '    2.2 总体架构设计',
        '    2.3 模块划分与关系',
        '    2.4 场景与界面系统设计',
        '    2.5 主循环与资源加载设计',
        '三、核心模块详细设计',
        '    3.1 游戏入口与页面切换',
        '    3.2 整理棋盘与过关判定',
        '    3.3 关卡机制设计',
        '    3.4 撤回、洗牌、交换与星级',
        '    3.5 关卡生成与可解性验证',
        '    3.6 体力设计',
        '    3.7 过关奖励、图纸与工坊',
        '    3.8 试衣间与立绘分层',
        '    3.9 签到、任务与游戏圈',
        '    3.10 同款装箱活动',
        '    3.11 排行榜与登录',
        '四、数据结构设计',
        '五、数据接口设计',
        '六、出错处理设计',
        '七、性能优化设计',
        '八、结论',
    ]:
        pdf.write_body(item)

    pdf.add_page()
    pdf.write_h1('一、引言')
    pdf.write_h2('1.1 编写目的')
    pdf.write_body(
        f'编写本设计说明书是{SOFTWARE_FULL_NAME} {SOFTWARE_VERSION}软件著作权登记材料的一部分。'
        '本文档用于说明本软件的功能范围、总体架构、核心模块、数据结构、接口设计、异常处理和性能方案，'
        '证明本软件为独立开发完成的原创游戏软件。'
    )
    pdf.write_body(
        '本文档面向软件著作权审查人员及后续维护人员，重点描述软件技术实现，不包含运营数据、用户隐私数据和商业敏感策略。'
    )

    pdf.write_h2('1.2 软件概述')
    pdf.write_body(
        '一裙又一裙是一款基于微信小游戏运行环境开发的竖屏整理换装游戏。棋盘上是一列列叠着的裙子。'
        '玩家点一列，把手里那件插到该列最上面，并把该列最下面那件顶到手里。'
        '全部列按款式归好、手里只剩问号礼盒时过关。礼盒翻开后解锁裙子、发型或图纸，'
        '材料在工坊里做成新衣服，再进试衣间给角色换装。'
    )
    pdf.write_body('本软件的主要功能包括:')
    for text in [
        '整理棋盘: 点列插顶、顶出列底，支持包裹、防尘罩、专属列、锁与钥匙、限时闹钟。',
        '千关战役: 前 30 关手排教学，其后按关卡号生成，共 1000 关。每关种子固定，倒推打乱并回放验证可解。',
        '局内辅助: 撤回上一步，每关有限次洗牌和交换。剩余步数决定一到三星。',
        '体力: 上限 5 点，每 5 分钟恢复 1 点，进关消耗 1 点，可看激励视频补充。',
        '收集与制作: 前 20 关直送装扮，第 21 关起发放图纸和八种材料，工坊按配方制作。',
        '试衣间: 裙子、发型、翅膀分页穿戴。立绘按翅膀、后发、衣服、前发分层叠放。',
        '日常: 七日签到、过关任务、游戏圈当日发帖领体力、每日同款装箱。',
        '排行: 已通关关数上报云端，只升不降。装扮与关卡进度保存在本地。',
    ]:
        pdf.write_bullet(text)

    pdf.write_h2('1.3 运行环境')
    pdf.write_table(
        ['项目', '说明'],
        [
            ['运行平台', '微信小游戏。编辑器内可直接运行同一套逻辑'],
            ['引擎', '团结引擎 1.6.x（Unity 2022.3 内核）'],
            ['开发语言', 'C#；云函数为 JavaScript'],
            ['画面', '竖屏。设计稿 1080x1920，预览 720x1280，按宽适配'],
            ['界面', '同一 Canvas 下的面板切换，预制外观加运行时填数据'],
            ['本地存档', 'PlayerPrefs，键名 dresssort.save.v2'],
            ['后端', '腾讯云 CloudBase 云函数 dresssort-api'],
            ['云端能力', '微信登录换 JWT、通关排行、游戏圈发帖校验'],
            ['资源', '包内小图先显示，立绘高清图、图标和音乐从 CDN 替换'],
        ],
        [32, CONTENT_W - 32],
    )

    pdf.write_h2('1.4 术语与缩略语')
    pdf.write_table(
        ['术语', '含义'],
        [
            ['列', '一列衣架。索引 0 是视觉上最上面一件'],
            ['手里', '玩家当前握着的那一件。过关时必须是问号礼盒'],
            ['问号礼盒', '编号等于款式数量的特殊件，整理完成后留在手里'],
            ['包裹', '发牌时盖住的格子，落到列底才露出款式'],
            ['防尘罩', '整列不能点、也看不见，叠好指定数量的其它列后拉开'],
            ['专属列', '列顶气泡指定款式，这一列只认这一款'],
            ['钥匙', '挂在某一件上。该件被顶出时，从左到右打开一把锁'],
            ['闹钟', '挂在某一件上。步数走到截止仍未被顶出则本局失败'],
            ['图纸', '第 21 关起发放的制作资格，在工坊消耗材料做成衣服'],
            ['装箱', '活动玩法。顶上连续同款倒到另一列，放满后装进收纳箱'],
        ],
        [28, CONTENT_W - 28],
    )

    pdf.write_h1('二、软件总体设计')
    pdf.write_h2('2.1 软件需求概括')
    pdf.write_body('本软件要同时满足下面几条:')
    for text in [
        '每一次点击只改变手里和被点的那一列，规则可以脱离界面单独推演。',
        '发牌从终局倒推，再按记录下的走法正着回放。回放在步数或闹钟上失败时换种子重试。',
        '同一关卡号永远得到同一盘棋，进度只记录打到第几关，不保存每一关的摆法。',
        '新机制先单独出现，再和已教过的机制组合。第 31 关以后难度按百关分档。',
        '衣服、材料和图纸的发放节奏集中在一张表里，调节奏不改界面代码。',
        '页面切换不加载场景。微信小游戏上切首页、关卡、领奖、试衣间不会卡一下。',
        '网络只负责登录、排行和游戏圈。断网时整理、换装和本地存档仍然可用。',
    ]:
        pdf.write_bullet(text)

    pdf.write_h2('2.2 总体架构设计')
    pdf.write_body(
        '软件分成规则层、内容层、界面层和平台层。规则层是纯 C#，不引用 UnityEngine。'
        'SortBoard 负责主线整理，PackBoard 负责活动装箱。'
        '内容层用 LevelCatalog、CraftCatalog、TaskCatalog 和 GameDatabase 提供关卡、配方、任务和物品。'
        '界面层把规则状态画成衣架、礼盒、试衣间和弹窗，不反向修改胜负。'
        '平台层负责体力存档、微信登录、激励视频、CDN 图片和云函数请求。'
    )
    pdf.write_body(
        '一次点击的数据流是: 界面收到列点击，调用 SortBoard 的移动接口，'
        '规则层返回这一步打开的罩、解开的锁、关掉的闹钟，以及是否胜利或失败。'
        '界面再播放插顶动画、飘字和音效。胜负以规则层返回值为准。'
    )

    pdf.write_h2('2.3 模块划分与关系')
    pdf.write_table(
        ['模块', '主要类型', '职责'],
        [
            ['入口', 'App', '建 Canvas，六个面板显示隐藏切换'],
            ['整理规则', 'SortBoard', '插列、顶出、机制、胜负、倒推发牌'],
            ['装箱规则', 'PackBoard', '同款倒列、装箱、补充、广告列'],
            ['存档', 'WardrobeService', '解锁、穿戴、体力、材料、图纸、任务'],
            ['关卡', 'LevelCatalog', '1000 关参数与棋盘用裙子'],
            ['奖励', 'CraftCatalog', '直送、图纸、八种材料掉落'],
            ['任务', 'TaskCatalog', '按通关关数排列的礼物'],
            ['棋盘视图', 'BoardView / PackView', '把规则状态画出来'],
            ['立绘', 'PaperDoll', '翅膀、后发、衣服、前发分层'],
            ['页面', 'Home / Game / Reward 等', '首页、关卡、领奖、换装、工坊、装箱'],
            ['平台', 'Backend / WxBridge / Cdn', '登录、排行、广告、高清图'],
            ['云函数', 'dresssort-api', '登录、排行、游戏圈解密'],
        ],
        [28, 48, CONTENT_W - 76],
    )

    pdf.write_h2('2.4 场景与界面系统设计')
    pdf.write_body(
        '工程只有一个场景。App 在同一个 Canvas 下建立首页、关卡、领奖、试衣间、装箱和工坊六个面板。'
        '切页面只改显示，不卸载场景。首页上的签到、排行、游戏圈、任务和体力不足，是盖在首页上的弹窗。'
        '刘海和胶囊由安全区把顶栏往下推，底图和衣柜面板铺满全屏。'
    )
    pdf.write_table(
        ['界面', '进入方式', '离开后去向'],
        [
            ['首页', '启动默认', '开始进关卡，装扮进试衣间，侧边进弹窗或活动'],
            ['关卡', '消耗 1 点体力', '过关进领奖或回首页；暂停可重开或回家'],
            ['领奖', '首次拿到衣服或图纸', '收下后回首页；工坊刚开放时可去工坊'],
            ['试衣间', '首页装扮按钮', '保存后回首页'],
            ['工坊', '通关第 21 关后', '制作或试穿，返回首页'],
            ['装箱', '首页活动入口', '装满三箱或主动返回'],
        ],
        [28, 52, CONTENT_W - 80],
    )

    pdf.write_h2('2.5 主循环与资源加载设计')
    pdf.write_body(
        '启动时 App 读取 GameDatabase，安装音效，预热立绘 CDN，创建 WardrobeService 并显示首页。'
        '关卡面板打开时按关卡号生成 LevelDef，SortBoard 从已解状态倒推发牌，BoardView 整盘重画。'
        '玩家操作在规则确认之后才播放动画。动画未结束时忽略下一拍点击，避免棋盘状态和画面脱节。'
    )
    pdf.write_body(
        '立绘在包里先放缩小图。CdnAssets 按清单把高清图拉下来再换上，同时最多两路，拉不到就继续用小图。'
        '编辑器直接读工程里的原图，方便截图自检。背景音乐按页面切换，首页、关卡和活动使用不同曲目。'
    )

    pdf.write_h1('三、核心模块详细设计')
    pdf.write_h2('3.1 游戏入口与页面切换')
    pdf.write_body(
        'App.Boot 只执行一次。它绑定界面切图，创建存档，清空旧界面，建立 Canvas 和六个面板，然后显示首页。'
        '面板都继承 Panel，Init 时建外观，OnShow 和 OnHide 负责填数据和停动画。'
        '当前关卡、刚通关的关卡、本关掉落的材料、回到首页要提示的一句话，以及是否弹出体力广告，都放在 App 上，页面之间用这些字段交接。'
    )
    pdf.write_body(
        '微信小游戏启动时先显示加载页。页面上方是游戏名称，中间是封面插画，底部显示加载进度，并给出健康游戏忠告全文。资源加载完成后进入首页。'
    )
    pdf.write_image(*img('loading'), max_h=108)
    pdf.write_image(*img('home'), max_h=108)
    pdf.write_body(
        '首页中央是当前穿戴的角色。顶部是体力，形如当前值与上限。'
        '开始按钮文案是下一关的关卡号，全部 1000 关通关后改为已通关。'
        '侧边入口分别打开签到、排行、游戏圈、工坊、任务和装箱。工坊在通关第 21 关之前点击只提示尚未开放。'
    )

    pdf.write_h2('3.2 整理棋盘与过关判定')
    pdf.write_body(
        'SortBoard 用数组保存每一列。一列是一个列表，索引 0 是最上面一件，最后一项是最下面一件，也是下一次会被顶出来的那件。'
        '玩家点一列时，手里那件插到列顶，列底那件变成新的手里物品，步数加一。'
        '手里那件落到中间后，若某列只剩一件异款、而这件正好是该列的款，异款会立刻转到列底，避免玩家再专门送一次。'
    )
    pdf.write_body(
        '每种款式的件数正好等于列高。因此只要每一列都是同款，各列款式必然互不相同，过关判定不必再做跨列查重。'
        '过关条件是: 手里是问号礼盒，并且每一列的高度等于列高、列内款式相同；专属列还要等于气泡指定的款式。'
    )
    pdf.write_image(*img('game'), max_h=108)
    pdf.write_body(
        '画面上，同一列越靠下的裙子压在上面那件的裙摆之上，玩家能看出下一个被顶出来的是最下面那件。'
        '已经归好的列显示完成标记，再点会提示这列已经叠好。防尘罩和锁不单独吃点击，点击仍然落到列的热区，由规则拒绝并给出原因。'
    )

    pdf.write_h2('3.3 关卡机制设计')
    pdf.write_body(
        '格子上的包裹、钥匙和闹钟编码在格子的整数值里，跟着裙子移动。列上的防尘罩、锁和专属款式记在列上，不跟单件走。'
        '前 30 关按固定顺序引入机制: 第 6 关包裹，第 11 关防尘罩，第 14 关专属列，第 19 关锁和钥匙，第 23 关限时闹钟。'
        '每种机制先单独出现，再和大盘以及别的机制混在一起。第一次遇到时，关卡顶部给出一句说明。'
    )

    pdf.write_h3('3.3.1 包裹')
    pdf.write_body(
        '发牌时按关卡配置盖住若干格。包裹在列中移动时仍然看不出款式。它变成某一列的最下面一件时拆开，并播放一次弹动。'
        '交换不能选中未拆开的包裹。这样玩家必须通过正常的插列把它送到列底。'
    )
    pdf.write_image(*img('parcel'), max_h=100)

    pdf.write_h3('3.3.2 防尘罩')
    pdf.write_body(
        '被罩住的列不能点击，里面的裙子也不绘制。吊牌上的数字是还要叠好几列其它列。'
        '其它列达到完成状态后数字下降，降到 0 时罩拉开，之后这一列一直可点。'
        '罩和锁、专属气泡都挂在衣架上，同一列只保留一个，避免标记叠在一起。'
    )
    pdf.write_image(*img('cover'), max_h=100)

    pdf.write_h3('3.3.3 专属列')
    pdf.write_body(
        '已解状态下第 c 列就是第 c 款，所以气泡上的款式等于列号。'
        '这一列只有整列都是该款才算完成。其它列可以自由归类，专属列用来卡住某一种裙子的去处。'
    )
    pdf.write_image(*img('target'), max_h=100)

    pdf.write_h3('3.3.4 锁和钥匙')
    pdf.write_body(
        '开局上锁的列看得见裙子，但不能点。带钥匙的那一件被顶到手里时，从左到右打开一把仍然锁着的列，并提示钥匙打开了一把锁。'
        '钥匙不能用交换直接换走，必须靠顶出来。防尘罩优先于锁: 同一列如果有罩，就不再上锁。'
    )
    pdf.write_image(*img('lock'), max_h=100)

    pdf.write_h3('3.3.5 限时闹钟')
    pdf.write_body(
        '闹钟挂在某一件的右侧，数字是截止步数。这件被顶出来时闹钟关掉。'
        '步数到达截止而这件还在列上或还在手里等待被插进去，本局失败。'
        '失败后可以撤回一步，把这件重新顶出来，或者整关重开。一关最多同时安排若干个闹钟，各自有自己的截止步。'
    )
    pdf.write_image(*img('alarm'), max_h=100)

    pdf.write_h2('3.4 撤回、洗牌、交换与星级')
    pdf.write_body(
        '每一步把被点的列，以及这一步自动归位的异款，压进历史栈。撤回弹出栈顶，把棋盘和手里恢复到上一步，并退回步数。'
        '没有历史时提示没有可撤回的步骤。闹钟已经响起或步数用完时，撤回仍然可用，让玩家有机会补救。'
    )
    pdf.write_body(
        '洗牌用另一个种子把这一关整盘重发，每关 3 次。对局还在进行时次数用完后按钮不再生效；'
        '闹钟响起或步数用完之后，洗牌会重新发一盘，方便换一种开局再打。'
        '交换每关 2 次。进入交换后，玩家点衣架上任意一格，用手里那件换走它。'
        '挂着钥匙或闹钟的格子、未拆的包裹、被罩住或锁住的列不能交换。交换成功同样可能拉开防尘罩或直接造成胜利。'
    )
    pdf.write_body(
        '胜利时星级按剩余步数计算。基础 1 星，剩余步数达到步数上限的三分之一再加 1 星，达到三分之二为 3 星。'
        '星数累加进存档，在试衣间顶部显示，用来表示整理完成得有多从容。'
    )

    pdf.write_h2('3.5 关卡生成与可解性验证')
    pdf.write_body(
        'LevelCatalog 不把 1000 关存成资源。Get 按关卡号算出列数、列高、打乱步数、步数上限、包裹数、罩、锁、专属列和闹钟数，再临时创建 LevelDef。'
        '棋盘上的裙子从固定的 8 款里按关卡号错开，取连续的若干款，所以相邻关卡的配色会变，但同一关永远相同。'
        '种子是关卡号的函数，发牌结果可重复。'
    )
    pdf.write_body(
        '发牌从已解开的终局开始: 每列都是同款，手里是问号礼盒。然后执行逆操作若干次，相当于从终局往回走。'
        '有防尘罩或锁时分两段倒推: 先只动没罩没锁的列，叠好够数的列拉开罩子、顶出钥匙开锁，再动其余的列。'
        '逆操作的序列被记下来，再正着回放一遍。回放要在步数上限内获胜，并且每个闹钟都在截止前被顶出。'
        '自动沉底让局面走岔、或者闹钟来不及关时，换下一个种子重来，最多 80 次。同一个种子永远发出同一盘。'
    )
    pdf.write_body(
        '第 31 关以后进入程序生成。每 100 关整体加难一档，列数和列高在 5 到 8 之间。'
        '每 10 关的第 10 关是难关，紧接着的两关降低机制数量。'
        '机制从已经教过的五种里抽取，越往后同时出现的种类越多，同一列仍然只挂一种衣架标记。开局至少留下三列可以走。'
    )

    pdf.write_h2('3.6 体力设计')
    pdf.write_body(
        '体力存在本地存档里，上限 5。时间戳记录上次结算的时刻，每 5 分钟恢复 1 点，恢复到上限后不再增加。'
        '进入关卡调用 SpendEnergy，成功才扣 1 点并打开关卡。当前不足时打开体力弹窗。'
        '看完激励视频增加 1 点，然后继续原先要点的那一关。广告中途关闭则不发体力，按钮恢复可点。'
    )
    pdf.write_body(
        '签到、游戏圈和装箱的奖励可以把体力加到上限以上。自然恢复只补到上限，避免挂机无限堆积，也允许玩家把活动奖励存下来连打几关。'
    )
    pdf.write_image(*img('energy'), max_h=100)

    pdf.write_h2('3.7 过关奖励、图纸与工坊')
    pdf.write_body(
        'CraftCatalog 决定每一关给什么。第 1 关到第 20 关是直送，其中第 10 关和第 20 关送发型，其余送裙子。'
        '第 21 关送出第一张裙子图纸，同时开放工坊，并把这张图纸需要的全部材料一次给齐，保证玩家马上能做一件。'
        '其后每隔若干关按裙子、发型和翅膀轮换发放图纸。某一类图纸发完后，该关改为多给一份材料。'
    )
    pdf.write_body(
        '材料有八种: 布匹、发丝、莓红、柠黄、湖蓝、墨黑四种染料、蕾丝、星砂。'
        '普通关掉落大约一组布匹、发丝和染料；每 10 关的难关额外给蕾丝和星砂。'
        '染料有较高概率掉在玩家当前最缺的颜色上，避免一张图纸因为单色卡死。'
        '一组 10 关的产出比对应图纸的消耗略多，图纸拿到后攒几关就能做。'
    )
    pdf.write_image(*img('reward'), max_h=100)
    pdf.write_body(
        '领奖页把闭合礼盒换成盒身和盒盖，盒盖飞走，同时放光。直送的显示衣服本身，图纸显示图纸卡，衣服小图印在卡上。'
        '只有材料、没有新衣服或新图纸的关卡不进领奖页，材料条在关卡上飘一会儿后回首页。'
    )
    pdf.write_body(
        '工坊按页签列出已获得的图纸。每张卡写着配方里每种材料的需求量和当前持有量，不够的数字用另一种颜色。'
        '材料够时可以制作，制作后该装扮解锁。已经拥有的卡显示试穿，直接进入对应穿戴。'
        '页签在打开时停在当前有能做的那一页。底部一条显示八种材料的库存。'
    )
    pdf.write_image(*img('workshop'), max_h=100)

    pdf.write_h2('3.8 试衣间与立绘分层')
    pdf.write_body(
        '装扮分裙子、翅膀、头发三个槽位。开局穿着默认裙子和默认发型。翅膀可以脱掉，裙子必须一直穿着一件。'
        '放进任务轨道里的衣服和头发，即使标记为初始物品，也要领了任务才算拥有，避免和任务奖励重复。'
        '试衣间只列出已经解锁的物品。点格子即穿上，返回首页时首页立绘使用同一套穿戴。'
    )
    pdf.write_image(*img('dressup'), max_h=100)
    pdf.write_image(*img('hair'), max_h=100)
    pdf.write_body(
        'PaperDoll 的叠放顺序固定为翅膀、后发、衣服、头和前发。各层顶边对齐同一张基准画布。'
        '正好基准高度时铺满槽位；脚超出基准时画布更高，多出来的部分从槽底伸出去，并画在衣柜面板上面，避免蕾丝把鞋盖住。'
        '包里的立绘是缩小图，高清图像素更多。缩放按固定的基准宽度计算，高清图到达后人物大小不变。'
    )

    pdf.write_h2('3.9 签到、任务与游戏圈')
    pdf.write_body(
        '七日签到的体力依次是 2、2、3、2、2、3、5。每天只能签一次。'
        '若昨天签过且这一轮未满 7 天，今天接在后面；断签一天，或者七天签满后再签，从第 1 天重新开始。'
        '弹窗上七张卡片显示天数和数量，已签的盖章，当天可签的卡片高亮。'
    )
    pdf.write_image(*img('checkin'), max_h=100)
    pdf.write_body(
        '过关任务是一条按关卡号排列的礼物。前 20 关隔几关就有一份，后面隔得更开。'
        '礼物可以是衣服、发型、一包材料或一张图纸。通关数达到要求后可以领取，领取记录写进存档，不能重复领。'
        '首页任务入口用还差几关作为提示；有可领的礼物时提示可以领取。'
    )
    pdf.write_image(*img('quest'), max_h=100)
    pdf.write_body(
        '游戏圈每天可领 3 点体力。客户端请微信返回当日发帖数据，云函数解密后得到发帖数。'
        '发帖数大于 0 才能领取。同一天领过之后按钮变为已领取。编辑器没有游戏圈数据时不阻塞其它本地功能。'
    )

    pdf.write_h2('3.10 同款装箱活动')
    pdf.write_body(
        '装箱规则在 PackBoard 中，不依赖 Unity。棋盘固定 6 列、每列最多 8 件。开局打开左边 3 列。'
        '先点一列，再点另一列: 只移动最上面连续的同款。目标列是空的，或者顶上也是同款，就可以倒。'
        '空位有几件就倒几件，剩下的留在原列。一列被同款放满后可以装箱，格子清空。'
        '收纳箱有 4 格，集满 4 格算完成 1 箱。完成第 1 箱时打开第 4 列。装满 3 箱过关。'
    )
    pdf.write_body(
        '第 5 列和第 6 列是广告列，看完激励视频后打开。补充裙子不要求台上先清空:'
        '有空位的已打开列各从备用队列叠上一件同款，原来的留在下面。'
        '这一局使用的裙子种类在开局确定。当天第一次装满 3 箱额外给 2 点体力，同一天再玩不再重复给。'
    )
    pdf.write_image(*img('pack'), max_h=100)

    pdf.write_h2('3.11 排行榜与登录')
    pdf.write_body(
        '通关时把已通关关数交给 RankService。Backend 先保证本地有未过期的 JWT。'
        '真机用微信登录得到的 code 调用云函数 /login；编辑器和其它环境使用本机匿名标识。'
        '令牌写入 PlayerPrefs。之后的请求带 Bearer。若返回 401，清掉令牌并重新登录一次。'
    )
    pdf.write_body(
        '云函数 /rank/submit 只在新成绩高于原成绩时更新，并可以一并写入昵称和头像。'
        '/rank/list 返回前若干名和自己的名次。排行弹窗把前三名标成金银铜，自己的一行固定在列表底部。'
        '尚未授权微信昵称时，底部提供使用微信昵称头像上榜。拉取失败时列表区提示稍后再试，不改本地通关进度。'
    )
    pdf.write_image(*img('rank'), max_h=100)
    pdf.write_body(
        '装扮、体力、材料和任务都在本地。云端不保存这些字段，因此换机不会自动带走衣柜。'
        '排行榜只表达通关进度，不作为存档本体。'
    )

    pdf.write_h1('四、数据结构设计')
    pdf.write_h2('4.1 本地存档')
    pdf.write_body('存档序列化为一条 JSON，键名 dresssort.save.v2。字段如下。')
    pdf.write_table(
        ['字段', '含义'],
        [
            ['unlocked', '已解锁物品 id 列表'],
            ['equippedDress / Wings / Hair', '当前三个槽位的物品 id'],
            ['levelsCleared', '已通关的最大关卡号'],
            ['stars', '累计星数'],
            ['energy / energyUtc', '当前体力与上次结算的 UTC 刻度'],
            ['checkRun / checkDay', '本轮已签天数与最后签到日期'],
            ['clubDay / packDay', '游戏圈和装箱奖励的领取日期'],
            ['blueprints', '已获得的图纸 id'],
            ['materials', '八种材料的数量'],
            ['taskClaimed', '已领取的任务下标'],
        ],
        [62, CONTENT_W - 62],
    )

    pdf.write_h2('4.2 一盘整理的状态')
    pdf.write_body(
        '每一格是一个整数。低 8 位是款式编号，更高的位分别表示包裹、钥匙和闹钟编号。'
        '问号礼盒的编号等于款式数量。空位用单独常量表示，不占用款式。'
        '列数组之外还有: 每列防尘罩还差的完成列数、是否已拉开、是否开局上锁、是否已开锁、专属款式，以及每个闹钟的截止步数。'
        '历史栈保存普通插列和交换。交换用行号区分，普通移动的行号为 -1。'
    )

    pdf.write_h2('4.3 关卡与奖励')
    pdf.write_body(
        'LevelDef 保存列数、列高、打乱步数、步数上限、种子、洗牌次数、交换次数、包裹数、闹钟数、罩、锁、专属标记，以及本关使用的裙子列表。'
        'reward 和 blueprint 二选一: 前 20 关填直送物品，其后填图纸物品。材料不写进关卡，结算时按关卡号和玩家缺口现算。'
    )
    pdf.write_code_block([
        'LevelReward { kind, itemId, hard, bigPack }',
        'kind: Materials | Gift | DressBlueprint | HairBlueprint | WingBlueprint',
        'materials[8]: cloth, hair, dyeRed, dyeYellow, dyeBlue, dyeBlack, lace, stardust',
    ])

    pdf.write_h2('4.4 装箱状态')
    pdf.write_body(
        '每一格记录款式和是否已翻开。六列各自有是否打开。'
        '备用队列保存还没叠上桌面的裙子。BoxFilled 是当前箱子里的格数，满 4 归零并把 BoxesDone 加一。'
        '选中列用下标表示，-1 表示还没有选。'
    )

    pdf.write_h1('五、数据接口设计')
    pdf.write_h2('5.1 规则接口')
    pdf.write_table(
        ['接口', '作用'],
        [
            ['SortBoard.Play(column)', '插顶并顶出，返回被顶出的款式'],
            ['SortBoard.Undo()', '恢复上一步'],
            ['SortBoard.Swap(column, row)', '用手中物品换走指定格'],
            ['SortBoard.Deal(...)', '从终局倒推发牌，并回放验证'],
            ['SortBoard.IsWin()', '手里为礼盒且每列已归类'],
            ['LevelCatalog.Get(db, index)', '按关卡号得到本关参数'],
            ['CraftCatalog.RewardFor / MaterialsFor', '本关图纸或直送，以及材料数量'],
            ['PackBoard.Tap / Pack / UnlockAd', '选列倒堆、装箱、打开广告列'],
        ],
        [78, CONTENT_W - 78],
    )

    pdf.write_h2('5.2 存档接口')
    pdf.write_table(
        ['接口', '作用'],
        [
            ['SpendEnergy / GainEnergy / RecoverEnergy', '扣体力、加体力、按时间恢复'],
            ['ReportCleared(level, stars)', '推进关卡进度并累加星数'],
            ['Unlock / Equip / Unequip', '解锁与穿戴。翅膀可脱，裙子不可空'],
            ['AddBlueprint / AddMaterials / CanCraft', '图纸、材料入库与是否可制作'],
            ['CheckIn / ClaimClub / ClaimPack / ClaimTask', '四类每日或进度奖励，各自防重复'],
        ],
        [78, CONTENT_W - 78],
    )

    pdf.write_h2('5.3 云端接口')
    pdf.write_body(
        '云函数入口按方法加路径分发。请求体是 JSON，成功时包装为 ok 与 data。'
        '登录签发 JWT。排行提交和列表、游戏圈解密都要求有效令牌。'
    )
    pdf.write_table(
        ['路径', '作用'],
        [
            ['POST /login', '微信 code 或匿名 id 换 token、userId、过期时间'],
            ['POST /rank/submit', '上报已通关关数，只升不降，可更新昵称头像'],
            ['POST /rank/list', '返回榜单前 N 名和自己的名次'],
            ['POST /gameclub/daily', '解密当日游戏圈数据，返回发帖数'],
            ['GET /health', '健康检查'],
        ],
        [52, CONTENT_W - 52],
    )
    pdf.write_body(
        '集合 dresssort_rankings 以 userId 唯一，按通关数降序、到达时间升序。'
        'dresssort_wxSessions 保存微信会话。游戏键、云函数目录名和云端环境变量 GAME_KEY 都是 dresssort。'
    )

    pdf.write_h2('5.4 平台接口')
    pdf.write_bullet('WxBridge.Login: 取微信登录 code。')
    pdf.write_bullet('WxBridge.ShowRewarded: 播放激励视频，完整观看才回调成功。')
    pdf.write_bullet('WxBridge.ShowClubButton: 在游戏圈按钮位置盖微信原生按钮。')
    pdf.write_bullet('CdnAssets.Bind: 按资源名替换 Image，失败保留包内小图。')

    pdf.write_h1('六、出错处理设计')
    pdf.write_h2('6.1 存档损坏与缺字段')
    pdf.write_body(
        '读取失败或 JSON 无法解析时，按新档初始化: 体力满、关卡为 0、穿上默认裙子和默认发型。'
        '旧档缺少材料数组或任务列表时补成空数组，不把整档作废。'
        '若当前穿着的衣服后来被改到任务里，加载时换回默认款，避免穿着一件尚未领取的衣服。'
    )

    pdf.write_h2('6.2 非法操作')
    pdf.write_body(
        '点到防尘罩、上锁的列、已归好的列、步数用尽或闹钟已响，规则层拒绝移动，界面用一句话说明原因。'
        '交换点到包裹、钥匙、闹钟或相同款式时同样拒绝，并保留剩余交换次数。'
        '洗牌、交换、签到、游戏圈、装箱奖励和任务在条件不满足时直接返回，不写存档。'
    )

    pdf.write_h2('6.3 关卡配置与发牌')
    pdf.write_body(
        '关卡号超出 1 到 1000，或数据库里缺少本关要用的裙子时，关卡面板提示配置不完整，不开始对局。'
        '倒推回放若不能在限制内获胜，发牌换种子再试。80 次仍未通过时，打开本关全部锁再把这一盘交给玩家，避免卡在加载。'
        '种子由关卡号决定，同一关的摆法可以重复得到。'
    )

    pdf.write_h2('6.4 网络、广告与资源')
    pdf.write_body(
        '登录超时、云函数返回错误或排行榜拉取失败时，只影响排行和游戏圈。本地整理和存档继续。'
        '令牌过期收到 401 后清缓存并重登一次，仍失败则本次上报放弃。'
        '激励视频未看完不发奖励。CDN 图片超时或失败后 30 秒内不再对同一张图重试，画面保持包内小图。'
        'GameDatabase 没有接上时，入口打出错误并停用组件，要求先在编辑器里重建资源与场景。'
    )

    pdf.write_h1('七、性能优化设计')
    pdf.write_h2('7.1 规则与绘制分离')
    pdf.write_body(
        'SortBoard 和 PackBoard 不创建 Unity 对象。发牌、回放和胜负都是内存里的整数运算。'
        '界面只在状态变化后重画受影响的列。整盘重画用于发牌、洗牌和撤回。'
        '自动归位的异款单独返回，视图可以只播那几件的下落，不必重建整个棋盘。'
    )

    pdf.write_h2('7.2 单场景与配置现算')
    pdf.write_body(
        '六个页面常驻在一个 Canvas 下，切页不触发场景加载，避免微信小游戏上的卡顿。'
        '1000 关不做成 1000 个资源文件。LevelDef 按关卡号现算并缓存，换数据库时清空缓存。'
        '关卡参数、配方和任务都是静态表，运行时不读表文件。'
    )

    pdf.write_h2('7.3 图片与请求')
    pdf.write_body(
        '高清立绘按需下载，并行数限制为 2，内存里保留最近使用的少量纹理。'
        '排行榜在通关时提交一次，不在每一步移动时访问网络。令牌在过期前一分钟内复用。'
        '音效预先装好，界面点击只播放已加载的短音，不在点击当帧读盘。'
    )

    pdf.write_h2('7.4 首包')
    pdf.write_body(
        '包内保留缩小的立绘和图标，保证首屏和断网时能画完整个人。'
        '高清图、部分图标和音乐放在 CDN。微信转换时加载封面使用单独的静图和视频，不把全部章节资源打进首包。'
    )

    pdf.write_h1('八、结论')
    pdf.write_body(
        f'{SOFTWARE_FULL_NAME} {SOFTWARE_VERSION}以列式整理为核心，把插顶规则、五种关卡机制、可解性回放、'
        '图纸工坊、分层换装和本地进度做成一套完整结构。'
        '规则层不依赖界面，关卡与奖励由关卡号和配置表驱动，云端只承担登录、排行和游戏圈。'
        '上述设计满足软件著作权登记文档鉴别材料对技术说明的要求。'
    )


def validate_pdf():
    from pypdf import PdfReader
    reader = PdfReader(str(OUTPUT))
    return len(reader.pages)


def main():
    pdf = DocPDF()
    pdf.add_font('Songti', '', SONGTI_PATH)
    write_document(pdf)
    pdf.output(str(OUTPUT))
    pages = validate_pdf()

    print('=' * 60)
    print('  一裙又一裙软著文档鉴别材料 PDF 生成报告')
    print('=' * 60)
    print(f'  软件名称:     {SOFTWARE_FULL_NAME} {SOFTWARE_VERSION}')
    print(f'  申请人:       {APPLICANT_NAME}')
    print(f'  项目路径:     {PROJECT_ROOT}')
    print(f'  文档类型:     设计说明书')
    print(f'  生成页数:     {pages} 页')
    print(f'  输出文件:     {OUTPUT}')
    if pdf.missing_images:
        print(f'  缺少截图:     {len(pdf.missing_images)} 张，PDF 中已使用占位框')
        for path, caption in pdf.missing_images:
            print(f'    - {Path(path).name}: {caption}')
    else:
        print('  截图检查:     已找到全部截图')
    print('=' * 60)


if __name__ == '__main__':
    main()

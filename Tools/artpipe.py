#!/usr/bin/env python3
"""叠叠裙美术管线。

生图约定（见 docs/prompt/）：AI 只画 #221A33 近黑紫轮廓线，不画白色贴纸边，
背景是一整片纯 #4A5A6B 灰蓝色。管线负责去底、统一画布、按用途补白边、压缩。

用法：
    artpipe.py cut  <in.png> <out.png>
    artpipe.py split <sheet.png> <out_dir> <cols> <rows> [--names a,b,c]
    artpipe.py ring <in.png> <out.png> --px 16
    artpipe.py pack <in.png> <out.png> --width 512 [--quant]
    artpipe.py ch1                      # 跑完第一章全流程
"""

import argparse
import json
import shutil
import subprocess
import sys
from pathlib import Path

import cv2
import numpy as np
from PIL import Image
from scipy import ndimage

ROOT = Path(__file__).resolve().parent.parent
RAW = ROOT / "game_assets_tmp" / "ch1"
ART = ROOT / "Assets" / "DressSort" / "Art"

BG_TOLERANCE = 70


# --------------------------------------------------------------------- 去底

def _background_mask(rgb: np.ndarray, tolerance: int = BG_TOLERANCE) -> np.ndarray:
    """背景 = 颜色接近四边中值、并且和画布边缘连通的区域。

    靠连通性而不是单纯的颜色判断，所以主体内部和背景同色的色块不会被打穿；
    近黑紫轮廓线形成闭合边界，泛洪正好停在轮廓线上。
    """
    border = np.concatenate([rgb[0], rgb[-1], rgb[:, 0], rgb[:, -1]])
    key = np.median(border, axis=0)
    candidate = np.abs(rgb.astype(np.int16) - key).sum(axis=2) < tolerance

    labels, count = ndimage.label(candidate)
    if count == 0:
        return np.zeros(rgb.shape[:2], bool)

    edge_labels = set(labels[0]) | set(labels[-1]) | set(labels[:, 0]) | set(labels[:, -1])
    edge_labels.discard(0)
    return np.isin(labels, list(edge_labels))


def cut(path: Path) -> Image.Image:
    rgb = np.asarray(Image.open(path).convert("RGB"))
    alpha = np.where(_background_mask(rgb), 0, 255).astype(np.uint8)
    return Image.fromarray(np.dstack([rgb, alpha]), "RGBA")


# ----------------------------------------------------------------- 几何操作

def content_box(im: Image.Image):
    alpha = np.asarray(im)[..., 3]
    ys, xs = np.where(alpha > 0)
    if len(xs) == 0:
        return None
    return int(xs.min()), int(ys.min()), int(xs.max()) + 1, int(ys.max()) + 1


def resize(im: Image.Image, size) -> Image.Image:
    """先预乘 alpha 再缩放，避免透明区域的背景色渗到边缘。"""
    a = np.asarray(im).astype(np.float32)
    alpha = a[..., 3:4] / 255.0
    premultiplied = np.dstack([a[..., :3] * alpha, a[..., 3]]).astype(np.uint8)
    scaled = np.asarray(
        Image.fromarray(premultiplied, "RGBA").resize(size, Image.LANCZOS)
    ).astype(np.float32)
    out_alpha = np.clip(scaled[..., 3:4], 0, 255)
    rgb = np.where(out_alpha > 0, scaled[..., :3] / np.maximum(out_alpha / 255.0, 1e-4), 0)
    return Image.fromarray(
        np.dstack([np.clip(rgb, 0, 255), out_alpha]).astype(np.uint8), "RGBA"
    )


def fit_height(im: Image.Image, canvas, target_height: int, anchor_y: float = 0.5):
    """把内容等比缩放到指定高度，放到统一画布上水平居中。

    棋盘上的裙子必须高度一致，否则整列会被最高的那件撑爆。
    """
    box = content_box(im)
    if box is None:
        return Image.new("RGBA", canvas, (0, 0, 0, 0))

    content = im.crop(box)
    scale = target_height / content.size[1]
    content = resize(content, (max(1, round(content.size[0] * scale)), target_height))

    out = Image.new("RGBA", canvas, (0, 0, 0, 0))
    out.alpha_composite(
        content,
        ((canvas[0] - content.size[0]) // 2,
         round((canvas[1] - content.size[1]) * anchor_y)),
    )
    return out


def white_ring(im: Image.Image, px: int) -> Image.Image:
    """在轮廓外补一圈等宽白边。

    棋盘上的裙子要叠六层，靠这圈白边把相邻两件隔开；角色立绘不补，
    否则换头和长发压裙子的接缝处会出现白豁口。
    """
    alpha = np.asarray(im)[..., 3]
    solid = (alpha > 96).astype(np.uint8) * 255
    kernel = cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (px * 2 + 1, px * 2 + 1))
    grown = cv2.dilate(solid, kernel)
    grown = cv2.GaussianBlur(grown, (3, 3), 0)

    ring = np.zeros((*alpha.shape, 4), np.uint8)
    ring[..., :3] = 255
    ring[..., 3] = grown

    out = Image.fromarray(ring, "RGBA")
    out.alpha_composite(im)
    return out


def despeckle(im: Image.Image, keep_ratio: float = 0.08) -> Image.Image:
    """丢掉零散的小碎片，只留主体。

    生图时轮廓外常会甩出几个小白点。它们本身看不太出来，但会把内容包围盒
    撑大，统一高度时整件就被缩小了，一列排下来大小参差不齐。
    """
    alpha = np.asarray(im)[..., 3]
    labels, count = ndimage.label(alpha > 8)
    if count <= 1:
        return im

    sizes = ndimage.sum(np.ones_like(labels), labels, range(1, count + 1))
    keep = [i + 1 for i, s in enumerate(sizes) if s >= sizes.max() * keep_ratio]

    a = np.asarray(im).copy()
    a[..., 3] = np.where(np.isin(labels, keep), alpha, 0)
    return Image.fromarray(a, "RGBA")


def split_sheet(path: Path, cols: int, rows: int):
    im = cut(path)
    w, h = im.size
    cw, ch = w // cols, h // rows
    cells = []
    for r in range(rows):
        for c in range(cols):
            cell = despeckle(im.crop((c * cw, r * ch, (c + 1) * cw, (r + 1) * ch)))
            box = content_box(cell)
            cells.append(cell.crop(box) if box else None)
    return cells


# -------------------------------------------------------------------- 输出

def save(im: Image.Image, path: Path, quant: bool = True) -> int:
    path.parent.mkdir(parents=True, exist_ok=True)
    im.save(path, optimize=True)
    if quant and shutil.which("pngquant"):
        subprocess.run(
            ["pngquant", "--quality=62-90", "--speed", "1", "--force",
             "--output", str(path), str(path)],
            check=False, capture_output=True,
        )
    return path.stat().st_size


# ------------------------------------------------------------- 第一章流程

DRESSES = [
    "rose_puff", "lemon_overall", "aqua_sailor", "lime_cami",
    "cream_offshoulder", "coral_square", "lilac_dot", "mint_high",
]
DRESS_LABELS = [
    "洋红泡泡袖裙", "柠檬黄背带裙", "电光青水手裙", "青柠绿吊带裙",
    "奶白一字肩裙", "橘橙方领裙", "淡紫圆点裙", "薄荷立领长袖裙",
]
UI_ICONS = [
    "wing_aqua", "wing_rose", "mystery", "star",
    "back", "gear", "lock", "check",
    "undo", "shuffle", "eject", "hanger",
]

PORTRAIT_WIDTH = 480
ICON_CANVAS = (256, 256)
ICON_HEIGHT = 216
WING_WIDTH = 360


def build_chapter_one():
    report = {}

    # 立绘：八张是从同一张基准图派生的，头部位置误差在 1px 内，
    # 所以用所有立绘的并集裁切框，保证运行时换装不会跳动。
    portraits = sorted(RAW.glob("portrait-*.png"))
    if not portraits:
        sys.exit("找不到立绘原图，先跑生图")

    cuts = {}
    boxes = []
    for p in portraits:
        im = cut(p)
        cuts[p] = im
        box = content_box(im)
        if box:
            boxes.append(box)
    union = (min(b[0] for b in boxes), min(b[1] for b in boxes),
             max(b[2] for b in boxes), max(b[3] for b in boxes))
    report["portrait_union_box"] = union

    for p, im in cuts.items():
        key = p.stem.split("-", 1)[1]
        cropped = im.crop(union)
        height = round(PORTRAIT_WIDTH * cropped.size[1] / cropped.size[0])
        out = resize(cropped, (PORTRAIT_WIDTH, height))
        size = save(out, ART / "Portraits" / f"{key}.png")
        report.setdefault("portraits", {})[key] = [out.size, size // 1024]

    # 裙子图标：统一高度后补白边，棋盘叠放才不会糊在一起
    cells = split_sheet(RAW / "sheet-dresses.png", 4, 2)
    for name, cell in zip(DRESSES, cells):
        if cell is None:
            continue
        framed = fit_height(cell, ICON_CANVAS, ICON_HEIGHT)
        size = save(white_ring(framed, 7), ART / "Icons" / f"dress_{name}.png")
        report.setdefault("icons", {})[name] = size // 1024

    # UI 图标：只取前三行，第四行是生图时多出来的废图
    ui = split_sheet(RAW / "sheet-ui.png", 4, 4)[:12]
    for name, cell in zip(UI_ICONS, ui):
        if cell is None:
            continue
        if name.startswith("wing_"):
            # 翅膀要两份：贴在角色背后的大图，和衣柜格子里的小图标
            height = round(WING_WIDTH * cell.size[1] / cell.size[0])
            save(resize(cell, (WING_WIDTH, height)), ART / "Wings" / f"{name}.png")
            framed = fit_height(cell, ICON_CANVAS, round(ICON_HEIGHT * 0.66))
            size = save(white_ring(framed, 7), ART / "Icons" / f"ui_{name}.png")
            report.setdefault("ui", {})[name] = size // 1024
            continue
        framed = fit_height(cell, (128, 128), 104)
        ring = 4 if name in ("mystery", "star", "lock") else 0
        out = white_ring(framed, ring) if ring else framed
        size = save(out, ART / "Icons" / f"ui_{name}.png")
        report.setdefault("ui", {})[name] = size // 1024

    # 问号礼盒同时要当棋盘上的一件，尺寸跟裙子图标对齐
    mystery = split_sheet(RAW / "sheet-ui.png", 4, 4)[2]
    if mystery is not None:
        framed = fit_height(mystery, ICON_CANVAS, round(ICON_HEIGHT * 0.82))
        save(white_ring(framed, 7), ART / "Icons" / "dress_mystery.png")

    print(json.dumps(report, ensure_ascii=False, indent=2))


# ------------------------------------------------------------- v7 粉发水手裙

V7 = ROOT.parent / "game_assets" / "game2D_cat" / "assets" / "raw" / "dresssort" / "v7"
if not V7.is_dir():
    V7 = ROOT / "game_assets_tmp" / "v7"

V7_DRESSES = [
    "teal_sailor", "black_ribbon", "pink_gingham", "lemon_print",
    "orange_slice", "ivory_lace", "strawberry", "grape_school",
]
V7_UI = [
    "star", "gear", "lock", "check",
    "back", "undo", "shuffle", "mystery",
    "wing_aqua", "wing_rose", "hanger", "gift",
]


def _cut_piece(src: Path) -> Image.Image:
    im = despeckle(cut(src))
    box = content_box(im)
    return im.crop(box) if box else im


def build_v7():
    if not V7.is_dir():
        sys.exit("找不到 v7 原图：" + str(V7))

    report = {"raw": str(V7)}

    portraits = sorted((V7 / "portraits").glob("*.png"))
    cuts = {}
    boxes = []
    for p in portraits:
        im = cut(p)
        cuts[p] = im
        box = content_box(im)
        if box:
            boxes.append(box)
    if not boxes:
        sys.exit("立绘去底后是空的")
    union = (min(b[0] for b in boxes), min(b[1] for b in boxes),
             max(b[2] for b in boxes), max(b[3] for b in boxes))
    report["portrait_union_box"] = union
    for p, im in cuts.items():
        cropped = im.crop(union)
        height = round(PORTRAIT_WIDTH * cropped.size[1] / cropped.size[0])
        out = resize(cropped, (PORTRAIT_WIDTH, height))
        size = save(out, ART / "Portraits" / f"{p.stem}.png")
        report.setdefault("portraits", {})[p.stem] = [list(out.size), size // 1024]

    cells = split_sheet(V7 / "sheet-dresses.png", 4, 2)
    for name, cell in zip(V7_DRESSES, cells):
        if cell is None:
            continue
        framed = fit_height(cell, ICON_CANVAS, ICON_HEIGHT)
        size = save(white_ring(framed, 7), ART / "Icons" / f"dress_{name}.png")
        report.setdefault("icons", {})[name] = size // 1024

    ui = split_sheet(V7 / "sheet-ui.png", 4, 3)
    for name, cell in zip(V7_UI, ui):
        if cell is None:
            continue
        if name.startswith("wing_"):
            height = round(WING_WIDTH * cell.size[1] / cell.size[0])
            save(resize(cell, (WING_WIDTH, height)), ART / "Wings" / f"{name}.png")
            framed = fit_height(cell, ICON_CANVAS, round(ICON_HEIGHT * 0.66))
            size = save(white_ring(framed, 7), ART / "Icons" / f"ui_{name}.png")
            report.setdefault("ui", {})[name] = size // 1024
            continue
        if name in ("mystery", "gift"):
            framed = fit_height(cell, ICON_CANVAS, round(ICON_HEIGHT * 0.82))
            dest = "dress_mystery" if name == "mystery" else "ui_gift"
            size = save(white_ring(framed, 7), ART / "Icons" / f"{dest}.png")
            report.setdefault("ui", {})[name] = size // 1024
            continue
        framed = fit_height(cell, (128, 128), 104)
        ring = 4 if name in ("star", "lock") else 0
        out = white_ring(framed, ring) if ring else framed
        size = save(out, ART / "Icons" / f"ui_{name}.png")
        report.setdefault("ui", {})[name] = size // 1024

    chrome = {
        "btn-teal": "btn_teal",
        "btn-pink": "btn_pink",
        "btn-white": "btn_white",
        "card": "card",
        "card-on": "card_on",
        "lane": "lane",
    }
    for src_name, dest_name in chrome.items():
        src = V7 / "ui" / f"{src_name}.png"
        if not src.exists():
            continue
        im = _cut_piece(src)
        size = save(im, ART / "Ui" / f"{dest_name}.png")
        report.setdefault("chrome", {})[dest_name] = [list(im.size), size // 1024]

    bg = V7 / "ui" / "bg.png"
    if bg.exists():
        im = Image.open(bg).convert("RGB").resize((1080, 1920), Image.LANCZOS)
        rgba = im.convert("RGBA")
        size = save(rgba, ART / "Ui" / "bg.png")
        report.setdefault("chrome", {})["bg"] = [[1080, 1920], size // 1024]

    print(json.dumps(report, ensure_ascii=False, indent=2))


# --------------------------------------------------------------------- CLI

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    sub = parser.add_subparsers(dest="cmd", required=True)

    p = sub.add_parser("cut"); p.add_argument("src"); p.add_argument("dst")
    p = sub.add_parser("ring"); p.add_argument("src"); p.add_argument("dst"); p.add_argument("--px", type=int, default=16)
    p = sub.add_parser("pack"); p.add_argument("src"); p.add_argument("dst"); p.add_argument("--width", type=int, default=512); p.add_argument("--quant", action="store_true")
    p = sub.add_parser("split"); p.add_argument("src"); p.add_argument("dst"); p.add_argument("cols", type=int); p.add_argument("rows", type=int)
    sub.add_parser("ch1")
    sub.add_parser("v7")

    args = parser.parse_args()
    if args.cmd == "cut":
        save(cut(Path(args.src)), Path(args.dst), quant=False)
    elif args.cmd == "ring":
        save(white_ring(Image.open(args.src).convert("RGBA"), args.px), Path(args.dst), quant=False)
    elif args.cmd == "pack":
        im = Image.open(args.src).convert("RGBA")
        height = round(args.width * im.size[1] / im.size[0])
        save(resize(im, (args.width, height)), Path(args.dst), quant=args.quant)
    elif args.cmd == "split":
        out = Path(args.dst); out.mkdir(parents=True, exist_ok=True)
        for i, cell in enumerate(split_sheet(Path(args.src), args.cols, args.rows)):
            if cell is not None:
                save(cell, out / f"cell_{i:02d}.png", quant=False)
    elif args.cmd == "ch1":
        build_chapter_one()
    elif args.cmd == "v7":
        build_v7()


if __name__ == "__main__":
    main()

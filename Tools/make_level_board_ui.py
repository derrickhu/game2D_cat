#!/usr/bin/env python3
"""对局页美术：房间底图自带横杆，衣架紧裁后钩子贴杆顶。

母版写到仓库外 game_assets，成品再拷进 Assets/DressSort/Art。
"""

from __future__ import annotations

import shutil
import uuid
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parent.parent
RAW_MOCK = Path(
    "/Users/rosa/rosa_games/game_assets/game2D_cat/assets/raw/dresssort/level-board-v1"
)
RAW_OUT = Path(
    "/Users/rosa/rosa_games/game_assets/game2D_cat/assets/raw/dresssort/level-board-ui"
)
ART = ROOT / "Assets" / "DressSort" / "Art"

W, H = 1080, 1920
RUNWAY_FIELD = (254, 208, 180)
RUNWAY_SIDE = (141, 211, 200)
INK = (34, 32, 36, 255)


def cover_to_board(im: Image.Image) -> Image.Image:
    """铺满 1080x1920，略裁上下，横杆位置跟原型一致。"""
    src = im.convert("RGB")
    sw, sh = src.size
    scale = max(W / sw, H / sh)
    nw, nh = max(1, int(round(sw * scale))), max(1, int(round(sh * scale)))
    src = src.resize((nw, nh), Image.LANCZOS)
    x0 = max(0, (nw - W) // 2)
    y0 = max(0, (nh - H) // 2)
    return src.crop((x0, y0, x0 + W, y0 + H))


def measure_rod_y(im: Image.Image) -> int:
    a = np.asarray(im.convert("RGB"))
    h, w = a.shape[:2]
    wood = (
        (a[:, :, 0] > 180)
        & (a[:, :, 0] < 245)
        & (a[:, :, 1] > 140)
        & (a[:, :, 1] < 220)
        & (a[:, :, 2] > 90)
        & (a[:, :, 2] < 190)
        & (a[:, :, 0] > a[:, :, 2] + 15)
    )
    row = wood.sum(1).astype(np.int32)
    row[int(h * 0.70) :] = 0
    return int(np.argmax(row))


def punch_paper(im: Image.Image) -> Image.Image:
    a = np.asarray(im.convert("RGB"))
    r, g, b = a[:, :, 0].astype(np.int16), a[:, :, 1].astype(np.int16), a[:, :, 2].astype(np.int16)
    mx = np.maximum(np.maximum(r, g), b)
    mn = np.minimum(np.minimum(r, g), b)
    sat = mx - mn
    bg = (mx > 235) & (sat < 28)
    bg |= (r > 245) & (g > 238) & (b > 225) & (sat < 40)
    alpha = np.where(bg, 0, 255).astype(np.uint8)
    return Image.fromarray(np.dstack([a, alpha]), "RGBA")


def tight_hanger(im: Image.Image) -> Image.Image:
    """钩子贴图顶，不再居中留白。"""
    punched = punch_paper(im)
    a = np.asarray(punched)
    ys, xs = np.where(a[:, :, 3] > 8)
    if len(xs) == 0:
        return Image.new("RGBA", (512, 320), (0, 0, 0, 0))
    crop = punched.crop((int(xs.min()), int(ys.min()), int(xs.max()) + 1, int(ys.max()) + 1))
    pad_t, pad_x, pad_b = 4, 8, 8
    out = Image.new("RGBA", (crop.size[0] + pad_x * 2, crop.size[1] + pad_t + pad_b), (0, 0, 0, 0))
    out.alpha_composite(crop, (pad_x, pad_t))
    scale = 768 / out.size[0]
    nw, nh = 768, max(1, int(round(out.size[1] * scale)))
    return out.resize((nw, nh), Image.LANCZOS)


def draw_rod_hanger() -> Image.Image:
    """原型镂空小木架：短钩贴顶，三角中空，挂在杆上。"""
    s = 4
    w, h = 720 * s, 420 * s
    im = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    wood = (226, 191, 146, 255)
    ink = (32, 28, 26, 255)
    cx = w // 2
    t = 10 * s
    # 短倒 U，卡在杆上
    d.arc((cx - 26 * s, 2 * s, cx + 26 * s, 50 * s), 200, 340, fill=ink, width=t)
    d.line((cx, 42 * s, cx, 68 * s), fill=ink, width=t)
    # 镂空三角：外沿填木色，内沿挖空
    top, bot = 66 * s, h - 14 * s
    left, right = 16 * s, w - 16 * s
    outer = [(cx, top), (right, bot - 20 * s), (right - 16 * s, bot),
             (left + 16 * s, bot), (left, bot - 20 * s)]
    d.polygon(outer, fill=wood)
    inset = 36 * s
    inner = [
        (cx, top + inset + 6 * s),
        (right - inset - 8 * s, bot - 28 * s),
        (left + inset + 8 * s, bot - 28 * s),
    ]
    hole = Image.new("L", (w, h), 0)
    ImageDraw.Draw(hole).polygon(inner, fill=255)
    rgba = np.asarray(im).copy()
    rgba[np.asarray(hole) > 0, 3] = 0
    im = Image.fromarray(rgba, "RGBA")
    d = ImageDraw.Draw(im)
    d.line(outer + [outer[0]], fill=ink, width=7 * s)
    d.line(inner + [inner[0]], fill=ink, width=6 * s)
    d.line((left + 24 * s, bot - 8 * s, right - 24 * s, bot - 8 * s), fill=ink, width=7 * s)
    r = 14 * s
    for x in (left + 12 * s, right - 12 * s):
        d.ellipse((x - r, bot - 18 * s - r, x + r, bot - 18 * s + r), fill=wood, outline=ink, width=7 * s)
    return im.resize((720, 420), Image.LANCZOS)


def key_near(rgb: np.ndarray, color, tol: int) -> np.ndarray:
    dist = np.abs(rgb.astype(np.int16) - np.array(color, dtype=np.int16)).sum(axis=2)
    return dist <= tol


def fit_square(im: Image.Image, size: int, margin: int) -> Image.Image:
    a = np.asarray(im)
    ys, xs = np.where(a[:, :, 3] > 8)
    if len(xs) == 0:
        return Image.new("RGBA", (size, size), (0, 0, 0, 0))
    content = im.crop((int(xs.min()), int(ys.min()), int(xs.max()) + 1, int(ys.max()) + 1))
    inner = size - margin * 2
    scale = min(inner / content.size[0], inner / content.size[1])
    nw, nh = max(1, int(content.size[0] * scale)), max(1, int(content.size[1] * scale))
    content = content.resize((nw, nh), Image.LANCZOS)
    out = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    out.alpha_composite(content, ((size - nw) // 2, (size - nh) // 2))
    return out


def extract_circles(src: Image.Image) -> list[Image.Image]:
    arr = np.asarray(src.convert("RGB"))
    h, w = arr.shape[:2]
    y0 = int(h * 0.84)
    region = arr[y0:]
    r, g, b = region[:, :, 0].astype(int), region[:, :, 1].astype(int), region[:, :, 2].astype(int)
    teal = (g > 90) & (g < 180) & (b > 90) & (b < 190) & (r < 110) & (g > r + 15)
    ys, xs = np.where(teal)
    hist = np.bincount(xs, minlength=w)
    clusters = []
    i = 0
    on = hist > 30
    while i < w:
        if on[i]:
            j = i
            while j < w and on[j]:
                j += 1
            if hist[i:j].sum() > 2000:
                clusters.append((i, j))
            i = j
        else:
            i += 1

    out = []
    for x0, x1 in clusters[:3]:
        col_ys = ys[(xs >= x0) & (xs < x1)]
        by0, by1 = int(col_ys.min()), int(col_ys.max()) + 1
        pad = 8
        crop = region[max(0, by0 - pad) : min(region.shape[0], by1 + 80), max(0, x0 - pad) : min(w, x1 + pad)]
        mask = key_near(crop, RUNWAY_FIELD, 48) | key_near(crop, RUNWAY_SIDE, 40)
        rgba = np.dstack([crop, np.where(mask, 0, 255).astype(np.uint8)])
        out.append(fit_square(Image.fromarray(rgba, "RGBA"), 512, 12))
    return out


def extract_gear(src: Image.Image) -> Image.Image:
    arr = np.asarray(src.convert("RGB"))
    h, w = arr.shape[:2]
    crop = arr[int(h * 0.028) : int(h * 0.078), int(w * 0.04) : int(w * 0.14)]
    wall = key_near(crop, (246, 245, 240), 30)
    rgba = np.dstack([crop, np.where(wall, 0, 255).astype(np.uint8)])
    return fit_square(Image.fromarray(rgba, "RGBA"), 256, 16)


def steps_capsule() -> Image.Image:
    size = (640, 176)
    scale = 4
    big = Image.new("RGBA", (size[0] * scale, size[1] * scale), (0, 0, 0, 0))
    d = ImageDraw.Draw(big)
    pad = 8 * scale
    d.rounded_rectangle(
        (pad, pad, size[0] * scale - pad, size[1] * scale - pad),
        radius=80 * scale,
        fill=(28, 28, 30, 255),
        outline=INK,
        width=6 * scale,
    )
    return big.resize(size, Image.LANCZOS)


def write_jpg(im: Image.Image, dest: Path):
    dest.parent.mkdir(parents=True, exist_ok=True)
    im.convert("RGB").save(dest, "JPEG", quality=90, optimize=True)


def write_png(im: Image.Image, dest: Path):
    dest.parent.mkdir(parents=True, exist_ok=True)
    im.save(dest, "PNG")


def copy_meta(template: Path, dest: Path, guid: str):
    text = template.read_text(encoding="utf-8")
    old = text.split("guid: ", 1)[1].splitlines()[0].strip()
    dest.write_text(text.replace(old, guid, 1), encoding="utf-8")


def compose_preview(bg: Image.Image, hanger: Image.Image, rod_y: int, dest: Path):
    """合成一张对照图，确认钩子坐在杆上。"""
    board = bg.convert("RGBA")
    hw, hh = 168, max(1, int(round(168 * hanger.size[1] / hanger.size[0])))
    token = hanger.resize((hw, hh), Image.LANCZOS)
    columns = 5
    pitch = 920 / columns
    for i in range(columns):
        x = int(W * 0.5 + (i - (columns - 1) * 0.5) * pitch - hw * 0.5)
        y = rod_y - 10
        board.alpha_composite(token, (x, y))
    write_png(board, dest)


def main():
    RAW_OUT.mkdir(parents=True, exist_ok=True)
    (RAW_OUT / ".write_test").write_text("ok\n", encoding="utf-8")

    closet_room = RAW_OUT / "bg_closet_empty.png"
    runway_room = RAW_OUT / "bg_runway_empty.png"
    if not closet_room.exists():
        closet_room = RAW_OUT / "bg_closet_room.png"
    if not runway_room.exists():
        runway_room = RAW_OUT / "bg_runway_room.png"

    closet = cover_to_board(Image.open(closet_room))
    runway = cover_to_board(Image.open(runway_room))
    rod_y = measure_rod_y(closet)
    print(f"closet rod_y={rod_y} uv={rod_y / H:.4f}")
    print(f"runway rod_y={measure_rod_y(runway)}")

    hanger_src = RAW_OUT / "hanger_only.png"
    if not hanger_src.exists():
        hanger_src = RAW_OUT / "hanger_gen.png"
    hanger = tight_hanger(Image.open(hanger_src))
    print(f"hanger {hanger.size} aspect={hanger.size[0] / hanger.size[1]:.3f}")

    raw_files: dict[str, Image.Image] = {
        "bg_closet.jpg": closet,
        "bg_runway.jpg": runway,
        "hanger_closet.png": hanger,
    }

    closet_src = Image.open(RAW_MOCK / "board_b_closet.png")
    runway_src = Image.open(RAW_MOCK / "board_d_runway7.png")
    if not (RAW_OUT / "btn_undo.png").exists():
        buttons = extract_circles(runway_src)
        if len(buttons) < 3:
            raise RuntimeError(f"expected 3 buttons, got {len(buttons)}")
        raw_files["btn_undo.png"] = buttons[0]
        raw_files["btn_shuffle.png"] = buttons[1]
        raw_files["btn_back.png"] = buttons[2]
        raw_files["btn_steps.png"] = steps_capsule()
        raw_files["btn_gear.png"] = extract_gear(closet_src)

    for name, im in raw_files.items():
        dest = RAW_OUT / name
        if name.endswith(".jpg"):
            write_jpg(im, dest)
        else:
            write_png(im, dest)

    compose_preview(closet, hanger, rod_y, RAW_OUT / "preview_closet_hangers.png")

    finals = {
        RAW_OUT / "bg_closet.jpg": ART / "Ui" / "Bgs" / "bg_closet.jpg",
        RAW_OUT / "bg_runway.jpg": ART / "Ui" / "Bgs" / "bg_runway.jpg",
        RAW_OUT / "hanger_closet.png": ART / "Ui" / "Game" / "hanger_closet.png",
    }
    for name in ("btn_undo.png", "btn_shuffle.png", "btn_back.png", "btn_steps.png", "btn_gear.png"):
        src = RAW_OUT / name
        if src.exists():
            finals[src] = ART / "Ui" / "Game" / name

    shutil.copy2(RAW_OUT / "hanger_closet.png", ART / "Icons" / "ui_hanger.png")

    jpg_meta = ART / "Ui" / "Bgs" / "bg_room.jpg.meta"
    png_meta = ART / "Ui" / "Home" / "btn_home_start.png.meta"
    for src, dest in finals.items():
        dest.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(src, dest)
        meta = dest.with_suffix(dest.suffix + ".meta")
        if not meta.exists():
            template = jpg_meta if dest.suffix == ".jpg" else png_meta
            copy_meta(template, meta, uuid.uuid4().hex)

    (RAW_OUT / ".write_test").unlink(missing_ok=True)
    print("raw:", RAW_OUT)
    for p in sorted(RAW_OUT.iterdir()):
        print(f"  {p.name:32} {p.stat().st_size:8d}")
    print(f"GameHud RodFromTop should be {rod_y}")
    print(f"GameHud hanger ~ 136 x {max(1, int(round(136 * hanger.size[1] / hanger.size[0])))}")


if __name__ == "__main__":
    main()

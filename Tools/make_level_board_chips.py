#!/usr/bin/env python3
"""把简化裙子小图切进 Icons，衣架紧裁后钩子贴顶。"""

from __future__ import annotations

import shutil
import sys
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parent.parent
sys.path.insert(0, str(ROOT / "Tools"))
import artpipe  # noqa: E402
from make_level_board_ui import tight_hanger  # noqa: E402

RAW_CHIPS = Path(
    "/Users/rosa/rosa_games/game_assets/game2D_cat/assets/raw/dresssort/level-board-chips"
)
RAW_UI = Path(
    "/Users/rosa/rosa_games/game_assets/game2D_cat/assets/raw/dresssort/level-board-ui"
)
ART = ROOT / "Assets" / "DressSort" / "Art"
ICON_RES = ROOT / "Assets" / "DressSort" / "Resources" / "DressIcons"

SHEET_NAMES = [
    "teal_sailor",
    "black_ribbon",
    "pink_gingham",
    "lemon_print",
    "orange_slice",
    "ivory_lace",
]
ROW3_NAMES = ["strawberry", "grape_school", "mystery"]


def pack_icon(cell: Image.Image, name: str) -> Image.Image:
    cell = artpipe.despeckle(cell)
    height = 200 if name == "mystery" else artpipe.ICON_HEIGHT
    framed = artpipe.fit_height(cell, artpipe.ICON_CANVAS, height)
    return framed


def main():
    sheet = RAW_CHIPS / "sheet_chips.png"
    row3 = RAW_CHIPS / "sheet_row3.png"
    cells = artpipe.split_sheet(sheet, 3, 3)
    extras = artpipe.split_sheet(row3, 3, 1) if row3.exists() else []

    out_dir = RAW_CHIPS / "final"
    out_dir.mkdir(parents=True, exist_ok=True)

    packed = {}
    for i, name in enumerate(SHEET_NAMES):
        if i >= len(cells) or cells[i] is None:
            raise RuntimeError(f"missing sheet cell {name}")
        packed[name] = pack_icon(cells[i], name)

    for i, name in enumerate(ROW3_NAMES):
        src = extras[i] if i < len(extras) and extras[i] is not None else (
            cells[6 + i] if 6 + i < len(cells) else None
        )
        if src is None:
            raise RuntimeError(f"missing row3 cell {name}")
        packed[name] = pack_icon(src, name)

    for name, im in packed.items():
        dest_raw = out_dir / f"dress_{name}.png"
        dest_art = ART / "Icons" / f"dress_{name}.png"
        dest_res = ICON_RES / f"{name}.png"
        artpipe.save(im, dest_raw, quant=False)
        dest_art.parent.mkdir(parents=True, exist_ok=True)
        dest_res.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(dest_raw, dest_art)
        shutil.copy2(dest_raw, dest_res)
        print(f"  dress_{name:16} {im.size} {dest_art.stat().st_size:7d}")

    hanger_src = RAW_UI / "hanger_only.png"
    hanger = tight_hanger(Image.open(hanger_src))
    hanger_raw = RAW_UI / "hanger_closet.png"
    hanger.save(hanger_raw)
    shutil.copy2(hanger_raw, ART / "Ui" / "Game" / "hanger_closet.png")
    shutil.copy2(hanger_raw, ART / "Icons" / "ui_hanger.png")
    print(f"  hanger {hanger.size} aspect={hanger.size[0] / hanger.size[1]:.3f}")
    print(f"GameHud size ~ 152 x {max(1, int(round(152 * hanger.size[1] / hanger.size[0])))}")


if __name__ == "__main__":
    main()

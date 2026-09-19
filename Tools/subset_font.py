#!/usr/bin/env python3
"""把 Noto Sans SC 裁到游戏里真正用到的字，塞进 Assets 当界面字体。

微信小游戏首包只有 4MB，完整的中文字体就有十几兆，必须做子集。
界面上加了新文案之后重跑一次这个脚本，字体会自动带上新字。

    python3 Tools/subset_font.py [--source /tmp/nsc.ttf]

字体是 Noto Sans SC，SIL Open Font License 1.1，可以随游戏分发。
"""

import argparse
import re
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
SCRIPTS = ROOT / "Assets" / "DressSort" / "Scripts"
OUT = ROOT / "Assets" / "DressSort" / "Resources" / "Fonts" / "UiFont.ttf"

# 界面上可能出现但不在代码字面量里的字符
EXTRA = (
    "0123456789"
    "abcdefghijklmnopqrstuvwxyz"
    "ABCDEFGHIJKLMNOPQRSTUVWXYZ"
    " .,:;!?+-*/%()[]{}<>=_'\"#&@~|\\^$"
    "：，。！？、；「」『』（）—…·×✓★☆♪"
    "第关星步件条已解锁全部收集奖励继续下一确定取消提示新手"
    "游戏圈排行榜签到工坊任务活动体力不足过一会儿再来即将开放补充设置稍后"
)

WEIGHT = 700


def collect_characters() -> set:
    """从 C# 字符串字面量里抓出所有要用到的字符。"""
    chars = set(EXTRA)
    for path in SCRIPTS.rglob("*.cs"):
        text = path.read_text(encoding="utf-8")
        for literal in re.findall(r'"((?:[^"\\\n]|\\.)*)"', text):
            chars.update(literal)
    # 编辑器脚本里的物品名同样会显示在界面上
    for path in (ROOT / "Assets" / "DressSort" / "Editor").rglob("*.cs"):
        text = path.read_text(encoding="utf-8")
        for literal in re.findall(r'"((?:[^"\\\n]|\\.)*)"', text):
            chars.update(literal)
    return {c for c in chars if c.isprintable() and c != " "} | {" "}


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--source", default="/tmp/nsc.ttf")
    args = parser.parse_args()

    source = Path(args.source)
    if not source.exists():
        sys.exit(f"找不到字体源文件 {source}，先下载 Noto Sans SC 可变字体")

    chars = collect_characters()
    cjk = sum(1 for c in chars if ord(c) > 0x2E80)
    print(f"用到 {len(chars)} 个字符，其中中日韩字形 {cjk} 个")

    OUT.parent.mkdir(parents=True, exist_ok=True)
    text = "".join(sorted(chars))

    subprocess.run(
        [sys.executable, "-m", "fontTools.varLib.instancer",
         str(source), f"wght={WEIGHT}", "-o", "/tmp/nsc-instance.ttf"],
        check=True, capture_output=True,
    )
    subprocess.run(
        [sys.executable, "-m", "fontTools.subset", "/tmp/nsc-instance.ttf",
         f"--text={text}", "--layout-features=*", "--no-hinting",
         "--desubroutinize", f"--output-file={OUT}"],
        check=True, capture_output=True,
    )

    print(f"{OUT.relative_to(ROOT)}  {OUT.stat().st_size // 1024} KB")


if __name__ == "__main__":
    main()

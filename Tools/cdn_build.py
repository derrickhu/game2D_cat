#!/usr/bin/env python3
"""长音乐和全身立绘 -> 云存储上的带 hash 成品 + C# 对照表。

    python3 Tools/cdn_build.py

音乐源在 CdnArt/，立绘源在 Assets/DressSort/Art/Portraits/。
成品在 Library/CdnOut/StreamingAssets。上传到云存储 dresssort/StreamingAssets，
路径里带 StreamingAssets，微信才会把下载结果缓存到本地。
内容变了 URL 就变，旧文件不用删，老版本客户端照样能拉。
"""

from __future__ import annotations

import hashlib
import shutil
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SRC = ROOT / "CdnArt"
PORTRAITS = ROOT / "Assets" / "DressSort" / "Art" / "Portraits"
OUT = ROOT / "Library" / "CdnOut" / "StreamingAssets"
MANIFEST = ROOT / "Assets" / "DressSort" / "Scripts" / "Platform" / "CdnManifest.cs"
BASE_URL = "https://726f-rosa-env-d7grf78r5dbd37323-1414200063.tcb.qcloud.la/dresssort/StreamingAssets/"
# 没进游戏的切图，不要上传
SKIP_PORTRAITS = {"portrait_orange_slice_body.png"}


def quant_png(src: Path, tmp: Path) -> None:
    q = subprocess.run(
        ["pngquant", "--quality=70-90", "--speed", "1", "--force", "--output", str(tmp), str(src)],
        capture_output=True,
    )
    if q.returncode not in (0, 99) or not tmp.exists() or tmp.stat().st_size <= 0:
        shutil.copyfile(src, tmp)


def add_file(entries: list, src: Path, rel: Path, quant: bool) -> None:
    name = rel.with_suffix("").as_posix()
    tmp = OUT / ("_tmp" + src.suffix.lower())
    if quant and src.suffix.lower() == ".png":
        quant_png(src, tmp)
    else:
        shutil.copyfile(src, tmp)
    data = tmp.read_bytes()
    digest = hashlib.md5(data).hexdigest()
    parent = "" if str(rel.parent) == "." else rel.parent.as_posix()
    url = f"{parent + '/' if parent else ''}{rel.stem}_{digest}{rel.suffix.lower()}"
    dst = OUT / url
    dst.parent.mkdir(parents=True, exist_ok=True)
    tmp.replace(dst)
    entries.append((name, url, dst.stat().st_size))


def main() -> int:
    if not SRC.is_dir():
        print("没有 CdnArt/", file=sys.stderr)
        return 1
    if OUT.exists():
        shutil.rmtree(OUT)
    OUT.mkdir(parents=True)
    entries = []
    for src in sorted(p for p in SRC.rglob("*") if p.is_file() and not p.name.startswith(".")):
        add_file(entries, src, src.relative_to(SRC), quant=False)
    if PORTRAITS.is_dir():
        for src in sorted(p for p in PORTRAITS.glob("*.png") if p.name not in SKIP_PORTRAITS):
            add_file(entries, src, Path("Portraits") / src.name, quant=True)
    entries.sort()

    lines = [
        "// 由 Tools/cdn_build.py 生成，别手改。",
        "using System.Collections.Generic;",
        "",
        "namespace DressSort",
        "{",
        "    public static class CdnManifest",
        "    {",
        f"        public const string BaseUrl = \"{BASE_URL}\";",
        "",
        "        // 逻辑名 -> 云上相对路径",
        "        public static readonly Dictionary<string, string> Files =",
        "            new Dictionary<string, string>",
        "            {",
    ]
    for name, url, _ in entries:
        lines.append(f"                {{ \"{name}\", \"{url}\" }},")
    lines += [
        "            };",
        "",
        "        public static string Url(string name)",
        "        {",
        "            string rel;",
        "            return Files.TryGetValue(name, out rel) ? BaseUrl + rel : null;",
        "        }",
        "    }",
        "}",
        "",
    ]
    MANIFEST.write_text("\n".join(lines), encoding="utf-8")

    total = 0
    for name, url, size in entries:
        total += size
        print(f"{size / 1024:7.1f} KB  {name}  ->  {url}")
    print(f"{len(entries)} files, {total / 1024 / 1024:.2f} MB -> {OUT}")
    return 0


if __name__ == "__main__":
    sys.exit(main())

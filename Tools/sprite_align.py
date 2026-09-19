#!/usr/bin/env python3
"""切图层对齐工具。

把你切好的头/身体/前后发一次导入多层，叠在基准全身像上互相对齐，
导出每张都是和基准同样尺寸的透明底 PNG。游戏里直接叠。

    python3 Tools/sprite_align.py
"""

from __future__ import annotations

import json
import mimetypes
import re
import sys
import webbrowser
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from urllib.parse import parse_qs, unquote, urlparse

ROOT = Path(__file__).resolve().parent.parent
ART = ROOT / "Assets" / "DressSort" / "Art"
HTML = Path(__file__).resolve().parent / "sprite_align.html"
OUT_DIR = ART / "Cast" / "aligned"
PORT = 8765

BASELINE_DIRS = [
    ART / "Cast" / "fullbody",
    ART / "Portraits",
]


def sanitize_name(name: str) -> str:
    name = Path(name).name
    name = re.sub(r"[^\w.\-\u4e00-\u9fff]+", "_", name)
    if not name.lower().endswith(".png"):
        name += ".png"
    return name or "aligned.png"


def unique_dest(name: str) -> Path:
    OUT_DIR.mkdir(parents=True, exist_ok=True)
    wanted = sanitize_name(name)
    dest = (OUT_DIR / wanted).resolve()
    if not safe_under(dest, OUT_DIR):
        raise ValueError("非法路径")
    if not dest.exists():
        return dest
    stem = dest.stem
    suffix = ""
    if stem.endswith("_864") or stem.endswith("_480"):
        suffix = stem[-4:]
        stem = stem[:-4]
    n = 2
    while True:
        cand = (OUT_DIR / f"{stem}_{n}{suffix}.png").resolve()
        if not cand.exists():
            return cand
        n += 1


def list_exports() -> list[str]:
    if not OUT_DIR.is_dir():
        return []
    files = [p for p in OUT_DIR.glob("*.png") if p.is_file()]
    files.sort(key=lambda p: p.stat().st_mtime, reverse=True)
    return [p.name for p in files]


def safe_under(path: Path, folder: Path) -> bool:
    try:
        path.resolve().relative_to(folder.resolve())
        return True
    except ValueError:
        return False


def list_baselines():
    files = []
    prefer = []
    for folder in BASELINE_DIRS:
        if not folder.is_dir():
            continue
        for path in sorted(folder.glob("*.png")):
            rel = path.relative_to(ART).as_posix()
            item = {"label": rel, "url": "/api/file?path=" + rel}
            if path.name.startswith("full_"):
                prefer.append(item)
            elif path.name == "body_sailor_f.png":
                prefer.append(item)
            else:
                files.append(item)
    # 全身去底图和基准身体排前面
    order = {name: i for i, name in enumerate(
        ["full_apricot.png", "body_sailor_f.png", "full_milktea.png",
         "full_wisteria.png", "full_caramel.png", "full_baguette.png",
         "full_denim.png"]
    )}
    prefer.sort(key=lambda x: order.get(Path(x["label"]).name, 99))
    return prefer + files


class Handler(BaseHTTPRequestHandler):
    def log_message(self, fmt, *args):
        sys.stderr.write("align: " + (fmt % args) + "\n")

    def _send(self, code, body, content_type="text/html; charset=utf-8"):
        data = body if isinstance(body, bytes) else body.encode("utf-8")
        self.send_response(code)
        self.send_header("Content-Type", content_type)
        self.send_header("Content-Length", str(len(data)))
        self.send_header("Cache-Control", "no-store")
        self.end_headers()
        self.wfile.write(data)

    def do_GET(self):
        parsed = urlparse(self.path)
        if parsed.path in ("/", "/index.html"):
            self._send(200, HTML.read_text(encoding="utf-8"))
            return
        if parsed.path == "/api/baselines":
            self._send(200, json.dumps({"files": list_baselines()}), "application/json")
            return
        if parsed.path == "/api/exports":
            self._send(200, json.dumps({"files": list_exports()}), "application/json")
            return
        if parsed.path == "/api/unique":
            raw_name = unquote(parse_qs(parsed.query).get("name", ["aligned.png"])[0])
            dest = unique_dest(raw_name)
            self._send(200, json.dumps({"name": dest.name}), "application/json")
            return
        if parsed.path == "/api/file":
            rel = unquote(parse_qs(parsed.query).get("path", [""])[0])
            path = (ART / rel).resolve()
            if not safe_under(path, ART) or not path.is_file():
                self._send(404, "not found", "text/plain")
                return
            mime = mimetypes.guess_type(path.name)[0] or "application/octet-stream"
            self._send(200, path.read_bytes(), mime)
            return
        self._send(404, "not found", "text/plain")

    def do_POST(self):
        if urlparse(self.path).path != "/api/save":
            self._send(404, json.dumps({"ok": False, "error": "unknown"}), "application/json")
            return
        length = int(self.headers.get("Content-Length", "0"))
        raw = self.rfile.read(length)
        ctype = self.headers.get("Content-Type", "")
        try:
            saved = _save_multipart(ctype, raw)
        except ValueError as exc:
            self._send(400, json.dumps({"ok": False, "error": str(exc)}), "application/json")
            return
        self._send(200, json.dumps({
            "ok": True,
            "paths": [item["path"] for item in saved],
            "names": [item["name"] for item in saved],
        }), "application/json")


def _save_multipart(content_type: str, raw: bytes) -> list:
    match = re.search(r"boundary=(.+)", content_type)
    if not match:
        raise ValueError("没有 boundary")
    boundary = b"--" + match.group(1).encode("utf-8")
    parts = raw.split(boundary)
    fields: dict[str, str] = {}
    files: dict[str, bytes] = {}
    for part in parts:
        if not part or part in (b"--\r\n", b"--"):
            continue
        head, _, body = part.partition(b"\r\n\r\n")
        body = body.rstrip(b"\r\n")
        if body.endswith(b"--"):
            body = body[:-2]
        header = head.decode("utf-8", "replace")
        name_m = re.search(r'name="([^"]+)"', header)
        if not name_m:
            continue
        name = name_m.group(1)
        if "filename=" in header:
            files[name] = body.lstrip(b"\r\n")
        else:
            fields[name] = body.decode("utf-8", "replace")

    OUT_DIR.mkdir(parents=True, exist_ok=True)
    saved = []
    pairs = [("file", fields.get("name", "aligned.png"))]
    if "gameFile" in files:
        pairs.append(("gameFile", fields.get("gameName", "aligned_480.png")))
    for key, filename in pairs:
        if key not in files:
            continue
        dest = unique_dest(filename)
        dest.write_bytes(files[key])
        saved.append({
            "path": str(dest.relative_to(ROOT)),
            "name": dest.name,
        })
    if not saved:
        raise ValueError("没有收到图片")
    return saved


def main():
    if not HTML.exists():
        sys.exit("找不到 Tools/sprite_align.html")
    OUT_DIR.mkdir(parents=True, exist_ok=True)
    server = ThreadingHTTPServer(("127.0.0.1", PORT), Handler)
    url = f"http://127.0.0.1:{PORT}/"
    print(f"对齐工具：{url}", flush=True)
    print(f"导出目录：{OUT_DIR.relative_to(ROOT)}", flush=True)
    try:
        webbrowser.open(url)
    except Exception:
        pass
    try:
        server.serve_forever()
    except KeyboardInterrupt:
        print("\n已关闭")


if __name__ == "__main__":
    main()

#!/usr/bin/env python3
"""Turn the flat-background sticker renders into uniform Unity sprites.

The raw art sits on one flat mint-green plate, so we key on green excess
(G above the average of R and B) rather than plain RGB distance: the wolf's
light grey body is only ~85 away from the key colour and a distance key eats
half of it, while its green excess is 0.

Every animal ends up on the same 512x512 canvas with the same content height,
which is what keeps the board columns looking even in game.
"""
import os

import numpy as np
from PIL import Image

RAW_DIR = "Assets/AnimalSort/Art/raw"
OUT_DIR = "Assets/AnimalSort/Resources/Animals"
CANVAS = 512
CONTENT = 430
DEADZONE = 0.18  # fraction of the key's green excess treated as fully opaque

NAMES = {"cow": "Cow", "pig": "Pig", "bear": "Bear", "cat": "Cat", "wolf": "Wolf"}


def green_excess(rgb):
    return rgb[..., 1] - 0.5 * (rgb[..., 0] + rgb[..., 2])


def chroma_key(path):
    rgb = np.asarray(Image.open(path).convert("RGB"), dtype=np.float32)

    border = np.concatenate([rgb[0], rgb[-1], rgb[:, 0], rgb[:, -1]])
    key = np.median(border, axis=0)
    key_excess = float(green_excess(key))

    low = DEADZONE * key_excess
    share = np.clip((green_excess(rgb) - low) / (key_excess - low), 0.0, 1.0)
    alpha = 1.0 - share

    # Unpremultiply against the key colour so edges keep no green fringe.
    safe = np.maximum(alpha, 1e-3)[..., None]
    colour = np.clip((rgb - share[..., None] * key) / safe, 0, 255)

    image = Image.fromarray(np.dstack([colour, alpha * 255.0]).astype(np.uint8), "RGBA")
    return image, key, key_excess


def tight_crop(image, threshold=32):
    mask = image.getchannel("A").point(lambda v: 255 if v > threshold else 0)
    box = mask.getbbox()
    return image.crop(box) if box else image


def main():
    os.makedirs(OUT_DIR, exist_ok=True)
    for raw, out in sorted(NAMES.items()):
        cut, key, key_excess = chroma_key(os.path.join(RAW_DIR, raw + ".png"))
        cut = tight_crop(cut)

        scale = CONTENT / max(cut.size)
        size = (max(1, round(cut.width * scale)), max(1, round(cut.height * scale)))
        cut = cut.resize(size, Image.LANCZOS)

        canvas = Image.new("RGBA", (CANVAS, CANVAS), (0, 0, 0, 0))
        canvas.paste(cut, ((CANVAS - cut.width) // 2, (CANVAS - cut.height) // 2))
        canvas.save(os.path.join(OUT_DIR, out + ".png"))

        alpha = np.asarray(canvas)[..., 3]
        print("%-5s key=%s excess=%.1f content=%s solid=%.1f%% semi=%.2f%%" % (
            out, key.astype(int), key_excess, size,
            100 * (alpha > 250).mean(), 100 * ((alpha > 8) & (alpha < 248)).mean()))


if __name__ == "__main__":
    main()

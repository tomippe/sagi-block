#!/usr/bin/env python3
"""Crop app icon, remove white background, emit multi-size ICO."""

from __future__ import annotations

from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "SagiBlock" / "Assets" / "app-icon-source.png"
OUT_ICO = ROOT / "SagiBlock" / "Assets" / "app.ico"
OUT_PNG = ROOT / "SagiBlock" / "Assets" / "app-icon.png"
ICO_SIZES = [(16, 16), (24, 24), (32, 32), (48, 48), (64, 64), (128, 128), (256, 256)]
WHITE_THRESHOLD = 248
PADDING = 2


def remove_white_background(img: Image.Image) -> Image.Image:
    rgba = img.convert("RGBA")
    pixels = rgba.load()
    width, height = rgba.size
    for y in range(height):
        for x in range(width):
            r, g, b, a = pixels[x, y]
            if r >= WHITE_THRESHOLD and g >= WHITE_THRESHOLD and b >= WHITE_THRESHOLD:
                pixels[x, y] = (r, g, b, 0)
    return rgba


def crop_to_content(img: Image.Image, padding: int) -> Image.Image:
    bbox = img.getbbox()
    if bbox is None:
        return img
    left, top, right, bottom = bbox
    left = max(0, left - padding)
    top = max(0, top - padding)
    right = min(img.width, right + padding)
    bottom = min(img.height, bottom + padding)
    return img.crop((left, top, right, bottom))


def make_square(img: Image.Image) -> Image.Image:
    width, height = img.size
    size = max(width, height)
    square = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    offset = ((size - width) // 2, (size - height) // 2)
    square.paste(img, offset, img)
    return square


def main() -> None:
    if not SOURCE.exists():
        raise SystemExit(f"Source not found: {SOURCE}")

    img = Image.open(SOURCE)
    img = remove_white_background(img)
    img = crop_to_content(img, PADDING)
    img = make_square(img)

    OUT_PNG.parent.mkdir(parents=True, exist_ok=True)
    img.save(OUT_PNG, format="PNG", optimize=True)

    resized = [img.resize(size, Image.Resampling.LANCZOS) for size in ICO_SIZES]
    resized[0].save(
        OUT_ICO,
        format="ICO",
        sizes=ICO_SIZES,
        append_images=resized[1:],
    )

    print(f"Saved: {OUT_PNG} ({img.size[0]}x{img.size[1]})")
    print(f"Saved: {OUT_ICO}")


if __name__ == "__main__":
    main()

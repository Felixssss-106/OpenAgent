#!/usr/bin/env python3
"""Render the tray icon used by the Windows shell.

The shell shows it on both light and dark taskbars, so the mark is a solid
accent tile with a light glyph: no single-tone icon survives both. Emitted as
a plain RGBA PNG (no deps) into the app's Assets folder.

    python scripts/gen-tray-icon.py
"""

import struct
import zlib
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
OUTPUT = ROOT / "src" / "apps" / "windows" / "OpenAgent.Windows" / "Assets" / "tray.png"

SIZE = 64
RADIUS = 16
# Accent blue from design/tokens.css (light palette); readable on both taskbars.
TILE = (0x2D, 0x67, 0xBB)
MARK = (0xFF, 0xFF, 0xFF)


def inside_rounded_square(x: int, y: int, size: int, radius: int) -> bool:
    """Point-in-shape test for a square with circular corners."""
    max_x = size - 1 - radius
    max_y = size - 1 - radius

    if x < radius and y < radius:  # top-left
        return (x - radius) ** 2 + (y - radius) ** 2 <= radius**2
    if x > max_x and y < radius:  # top-right
        return (x - max_x) ** 2 + (y - radius) ** 2 <= radius**2
    if x < radius and y > max_y:  # bottom-left
        return (x - radius) ** 2 + (y - max_y) ** 2 <= radius**2
    if x > max_x and y > max_y:  # bottom-right
        return (x - max_x) ** 2 + (y - max_y) ** 2 <= radius**2

    return True


def inside_mark(x: int, y: int, size: int) -> bool:
    """A ring: reads as an agent 'eye' at 16px without turning into a blob."""
    cx = cy = (size - 1) / 2
    distance = ((x - cx) ** 2 + (y - cy) ** 2) ** 0.5
    return 8.0 <= distance <= 15.0


def build_pixels() -> bytearray:
    raw = bytearray()
    for y in range(SIZE):
        raw.append(0)  # filter type: None
        for x in range(SIZE):
            if not inside_rounded_square(x, y, SIZE, RADIUS):
                raw.extend((0, 0, 0, 0))
            elif inside_mark(x, y, SIZE):
                raw.extend((*MARK, 0xFF))
            else:
                raw.extend((*TILE, 0xFF))
    return raw


def chunk(tag: bytes, data: bytes) -> bytes:
    return (
        struct.pack(">I", len(data))
        + tag
        + data
        + struct.pack(">I", zlib.crc32(tag + data) & 0xFFFFFFFF)
    )


def main() -> int:
    header = struct.pack(">IIBBBBB", SIZE, SIZE, 8, 6, 0, 0, 0)
    png = (
        b"\x89PNG\r\n\x1a\n"
        + chunk(b"IHDR", header)
        + chunk(b"IDAT", zlib.compress(bytes(build_pixels()), 9))
        + chunk(b"IEND", b"")
    )

    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    OUTPUT.write_bytes(png)
    print(f"wrote {OUTPUT.relative_to(ROOT)} ({len(png)} bytes, {SIZE}x{SIZE})")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

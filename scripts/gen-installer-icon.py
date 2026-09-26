#!/usr/bin/env python3
"""Render the multi-size .ico used by the Windows installer.

The tray glyph (scripts/gen-tray-icon.py) is a single 64px PNG, which is the
wrong shape for an installer: shortcuts, the taskbar and Add/Remove Programs
each pick a different size from one .ico, and a lone 64px entry gets resampled
by the shell. So the same mark is re-rendered proportionally at the standard
sizes and packed as PNG-in-ICO (supported since Vista).

    python scripts/gen-installer-icon.py
"""

import struct
import zlib
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
OUTPUT = ROOT / "installer" / "app.ico"

SIZES = (16, 24, 32, 48, 64, 128, 256)
# Accent blue from design/tokens.css (light palette); readable on both taskbars.
TILE = (0x2D, 0x67, 0xBB)
MARK = (0xFF, 0xFF, 0xFF)

# Fractions of the canvas, matching the 64px tray mark: 16/64 corner radius,
# 8/64..15/64 ring.
CORNER_RADIUS = 16 / 64
RING_INNER = 8 / 64
RING_OUTER = 15 / 64

# Extra samples per axis; a 4x grid box-filters to a clean edge at 16px.
SUPERSAMPLE = 4


def sample(px: float, py: float, size: int) -> tuple[int, int, int, int]:
    """Colour of one sub-pixel, in canvas coordinates."""
    radius = size * CORNER_RADIUS
    max_xy = size - 1 - radius
    cx = cy = (size - 1) / 2

    if px < radius and py < radius:  # top-left
        inside = (px - radius) ** 2 + (py - radius) ** 2 <= radius**2
    elif px > max_xy and py < radius:  # top-right
        inside = (px - max_xy) ** 2 + (py - radius) ** 2 <= radius**2
    elif px < radius and py > max_xy:  # bottom-left
        inside = (px - radius) ** 2 + (py - max_xy) ** 2 <= radius**2
    elif px > max_xy and py > max_xy:  # bottom-right
        inside = (px - max_xy) ** 2 + (py - max_xy) ** 2 <= radius**2
    else:
        inside = True

    if not inside:
        return 0, 0, 0, 0

    distance = ((px - cx) ** 2 + (py - cy) ** 2) ** 0.5 / size
    if RING_INNER <= distance <= RING_OUTER:
        return (*MARK, 0xFF)
    return (*TILE, 0xFF)


def render(size: int) -> bytes:
    """Box-filtered RGBA raster for one icon size."""
    step = 1 / SUPERSAMPLE
    raw = bytearray()
    for y in range(size):
        raw.append(0)  # filter type: None
        for x in range(size):
            red = green = blue = alpha = 0.0
            for sy in range(SUPERSAMPLE):
                for sx in range(SUPERSAMPLE):
                    r, g, b, a = sample(x + sx * step, y + sy * step, size)
                    red += r
                    green += g
                    blue += b
                    alpha += a
            n = SUPERSAMPLE * SUPERSAMPLE
            raw.extend((round(red / n), round(green / n), round(blue / n), round(alpha / n)))
    return bytes(raw)


def chunk(tag: bytes, data: bytes) -> bytes:
    return (
        struct.pack(">I", len(data))
        + tag
        + data
        + struct.pack(">I", zlib.crc32(tag + data) & 0xFFFFFFFF)
    )


def png(size: int) -> bytes:
    header = struct.pack(">IIBBBBB", size, size, 8, 6, 0, 0, 0)
    return (
        b"\x89PNG\r\n\x1a\n"
        + chunk(b"IHDR", header)
        + chunk(b"IDAT", zlib.compress(render(size), 9))
        + chunk(b"IEND", b"")
    )


def main() -> int:
    images = [(size, png(size)) for size in SIZES]

    entries = b""
    offset = 6 + 16 * len(images)
    for size, data in images:
        dimension = 0 if size >= 256 else size
        entries += struct.pack("<BBBBHHII", dimension, dimension, 0, 0, 1, 32, len(data), offset)
        offset += len(data)

    ico = struct.pack("<HHH", 0, 1, len(images)) + entries + b"".join(d for _, d in images)
    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    OUTPUT.write_bytes(ico)
    sizes = ", ".join(str(s) for s, _ in images)
    print(f"wrote {OUTPUT.relative_to(ROOT)} ({len(ico)} bytes, sizes: {sizes})")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

#!/usr/bin/env python3
"""The OpenAgent mark, rendered procedurally so every platform ships the same glyph.

One source of truth for shape and colour: Windows tray PNG, installer .ico and the
Android launcher icons are all generated from here. No third-party image library —
output is plain RGBA PNG written with struct/zlib.

The mark is a solid accent tile with a light ring. A ring rather than a dot because
at 16px a filled blob loses its edge on both light and dark surfaces, and the tile is
accent-coloured rather than single-tone so it survives both taskbar themes.
"""

import struct
import zlib

# Accent blue from design/tokens.css (light palette).
TILE = (0x2D, 0x67, 0xBB)
MARK = (0xFF, 0xFF, 0xFF)

# Fractions of the canvas, matching the original 64px tray mark.
CORNER_RADIUS = 16 / 64
RING_INNER = 8 / 64
RING_OUTER = 15 / 64

SUPERSAMPLE = 4


def sample(
    px: float, py: float, size: int, glyph_only: bool, shape: str, ring_scale: float
) -> tuple[int, int, int, int]:
    """Colour of one sub-pixel in canvas coordinates."""
    radius = size * CORNER_RADIUS
    max_xy = size - 1 - radius
    cx = cy = (size - 1) / 2

    if shape == "circle":
        inside = (px - cx) ** 2 + (py - cy) ** 2 <= (size / 2 - 0.5) ** 2
    elif px < radius and py < radius:  # top-left
        inside = (px - radius) ** 2 + (py - radius) ** 2 <= radius**2
    elif px > max_xy and py < radius:  # top-right
        inside = (px - max_xy) ** 2 + (py - radius) ** 2 <= radius**2
    elif px < radius and py > max_xy:  # bottom-left
        inside = (px - radius) ** 2 + (py - max_xy) ** 2 <= radius**2
    elif px > max_xy and py > max_xy:  # bottom-right
        inside = (px - max_xy) ** 2 + (py - max_xy) ** 2 <= radius**2
    else:
        inside = True

    distance = ((px - cx) ** 2 + (py - cy) ** 2) ** 0.5 / size / ring_scale
    in_ring = RING_INNER <= distance <= RING_OUTER

    if glyph_only:
        return (*MARK, 0xFF) if in_ring else (0, 0, 0, 0)
    if not inside:
        return 0, 0, 0, 0
    return (*MARK, 0xFF) if in_ring else (*TILE, 0xFF)


def render(
    size: int,
    glyph_only: bool = False,
    shape: str = "squircle",
    ring_scale: float = 1.0,
) -> bytes:
    """Box-filtered RGBA raster, PNG scanline-prefixed."""
    step = 1 / SUPERSAMPLE
    raw = bytearray()
    for y in range(size):
        raw.append(0)  # filter type: None
        for x in range(size):
            red = green = blue = alpha = 0.0
            for sy in range(SUPERSAMPLE):
                for sx in range(SUPERSAMPLE):
                    r, g, b, a = sample(
                        x + sx * step, y + sy * step, size, glyph_only, shape, ring_scale
                    )
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


def png(
    size: int,
    glyph_only: bool = False,
    shape: str = "squircle",
    ring_scale: float = 1.0,
) -> bytes:
    header = struct.pack(">IIBBBBB", size, size, 8, 6, 0, 0, 0)
    return (
        b"\x89PNG\r\n\x1a\n"
        + chunk(b"IHDR", header)
        + chunk(b"IDAT", zlib.compress(render(size, glyph_only, shape, ring_scale), 9))
        + chunk(b"IEND", b"")
    )

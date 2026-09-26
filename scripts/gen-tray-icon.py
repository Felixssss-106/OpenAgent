#!/usr/bin/env python3
"""Render the tray icon used by the Windows shell.

    python scripts/gen-tray-icon.py

Shape and colour come from scripts/oa_mark.py, which the installer .ico and the
Android launcher icons also use.

The tray needs a real .ico, not the PNG this script used to emit: H.NotifyIcon
converts an ``IconSource`` into a GDI icon and throws on a bare PNG ("Argument
'picture' must be a picture that can be used as a Icon"), so the tray silently
disappeared. Entries are packed as classic 32bpp DIBs rather than PNG-in-ICO
(the installer icon does that for the shell) because the tray path goes through
``new Icon(stream)``, which reads DIBs on every Windows version.
"""

import struct
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import oa_mark  # noqa: E402

ROOT = Path(__file__).resolve().parent.parent
OUTPUT = ROOT / "src" / "apps" / "windows" / "OpenAgent.Windows" / "Assets" / "tray.ico"

SIZES = (16, 24, 32, 48)


def to_bgra(row: bytes) -> bytes:
    out = bytearray(len(row))
    for i in range(0, len(row), 4):
        out[i] = row[i + 2]
        out[i + 1] = row[i + 1]
        out[i + 2] = row[i]
        out[i + 3] = row[i + 3]
    return bytes(out)


def dib(size: int) -> bytes:
    """A BITMAPINFOHEADER with bottom-up BGRA pixels and an empty AND mask."""
    raw = oa_mark.render(size)
    stride = size * 4 + 1  # PNG scanlines carry a filter byte
    rows = [raw[y * stride + 1:(y + 1) * stride] for y in range(size)]
    xor = b"".join(to_bgra(row) for row in reversed(rows))

    mask_stride = ((size + 31) // 32) * 4
    mask = b"\x00" * (mask_stride * size)  # all-transparent comes from alpha, not the mask

    header = struct.pack(
        "<IiiHHIIiiII",
        40, size, size * 2, 1, 32, 0, len(xor) + len(mask), 0, 0, 0, 0,
    )
    return header + xor + mask


def main() -> int:
    images = [(size, dib(size)) for size in SIZES]

    entries = b""
    offset = 6 + 16 * len(images)
    for size, data in images:
        entries += struct.pack("<BBBBHHII", size, size, 0, 0, 1, 32, len(data), offset)
        offset += len(data)

    ico = struct.pack("<HHH", 0, 1, len(images)) + entries + b"".join(d for _, d in images)
    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    OUTPUT.write_bytes(ico)
    print(f"wrote {OUTPUT.relative_to(ROOT)} ({len(ico)} bytes, sizes: {' '.join(map(str, SIZES))})")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

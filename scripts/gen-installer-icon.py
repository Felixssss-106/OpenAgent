#!/usr/bin/env python3
"""Render the multi-size .ico used by the Windows installer.

    python scripts/gen-installer-icon.py

The tray glyph (scripts/gen-tray-icon.py) is a single 64px PNG, which is the wrong
shape for an installer: shortcuts, the taskbar and Add/Remove Programs each pick a
different size out of one .ico, and a lone 64px entry gets resampled by the shell.
So the same mark is re-rendered at the standard sizes and packed as PNG-in-ICO
(supported since Vista). Shape and colour come from scripts/oa_mark.py, which the
Android launcher icons share.
"""

import struct
from pathlib import Path

import sys

sys.path.insert(0, str(Path(__file__).resolve().parent))
import oa_mark  # noqa: E402

ROOT = Path(__file__).resolve().parent.parent
OUTPUT = ROOT / "installer" / "app.ico"

SIZES = (16, 24, 32, 48, 64, 128, 256)


def main() -> int:
    images = [(size, oa_mark.png(size)) for size in SIZES]

    entries = b""
    offset = 6 + 16 * len(images)
    for size, data in images:
        dimension = 0 if size >= 256 else size
        entries += struct.pack("<BBBBHHII", dimension, dimension, 0, 0, 1, 32, len(data), offset)
        offset += len(data)

    ico = struct.pack("<HHH", 0, 1, len(images)) + entries + b"".join(d for _, d in images)
    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    OUTPUT.write_bytes(ico)
    print(f"wrote {OUTPUT.relative_to(ROOT)} ({len(ico)} bytes, sizes: {' '.join(map(str, SIZES))})")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

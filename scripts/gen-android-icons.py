#!/usr/bin/env python3
"""Generate the Android launcher icons from the same mark Windows ships.

    python scripts/gen-android-icons.py

Writes legacy mipmaps (pre-API 26), adaptive-icon foregrounds (API 26+, glyph only
— the accent tile becomes the background colour layer so launchers can mask it),
and the round variant. The adaptive foreground keeps the ring inside the 66dp safe
zone of the 108dp canvas, so no launcher mask clips it.
"""

import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import oa_mark  # noqa: E402

ROOT = Path(__file__).resolve().parent.parent
RES = ROOT / "android" / "app" / "src" / "main" / "res"

# density bucket -> (legacy icon px, adaptive foreground px)
DENSITIES = {
    "mdpi": (48, 108),
    "hdpi": (72, 162),
    "xhdpi": (96, 216),
    "xxhdpi": (144, 324),
    "xxxhdpi": (192, 432),
}

# Adaptive launchers mask the outer part of the 108dp canvas away; only the central
# 66dp (~61%) is guaranteed visible. The legacy tile draws its ring at ~47% of the
# canvas, so scale the foreground ring up to fill more of the safe zone without
# crossing it: 2 * RING_OUTER(0.234) * 1.2 = 56%.
FOREGROUND_RING_SCALE = 1.2

ADAPTIVE_XML = """<?xml version="1.0" encoding="utf-8"?>
<adaptive-icon xmlns:android="http://schemas.android.com/apk/res/android">
    <background android:drawable="@color/ic_launcher_background" />
    <foreground android:drawable="@mipmap/ic_launcher_foreground" />
    <monochrome android:drawable="@mipmap/ic_launcher_foreground" />
</adaptive-icon>
"""

COLORS_XML = """<?xml version="1.0" encoding="utf-8"?>
<resources>
    <!-- Accent from design/tokens.css; must match the tile in scripts/oa_mark.py. -->
    <color name="ic_launcher_background">#{tile_hex}</color>
</resources>
"""


def main() -> int:
    written = 0

    for bucket, (legacy_px, foreground_px) in DENSITIES.items():
        folder = RES / f"mipmap-{bucket}"
        folder.mkdir(parents=True, exist_ok=True)
        for name, data in (
            ("ic_launcher.png", oa_mark.png(legacy_px)),
            ("ic_launcher_round.png", oa_mark.png(legacy_px, shape="circle")),
            ("ic_launcher_foreground.png", oa_mark.png(foreground_px, glyph_only=True, ring_scale=FOREGROUND_RING_SCALE)),
        ):
            (folder / name).write_bytes(data)
            written += 1

    anydpi = RES / "mipmap-anydpi-v26"
    anydpi.mkdir(parents=True, exist_ok=True)
    for name in ("ic_launcher.xml", "ic_launcher_round.xml"):
        (anydpi / name).write_text(ADAPTIVE_XML, encoding="utf-8")
        written += 1

    values = RES / "values"
    values.mkdir(parents=True, exist_ok=True)
    tile_hex = "".join(f"{c:02X}" for c in oa_mark.TILE)
    (values / "ic_launcher_background.xml").write_text(
        COLORS_XML.format(tile_hex=tile_hex), encoding="utf-8"
    )
    written += 1

    print(f"wrote {written} file(s) under {RES.relative_to(ROOT)} (background #{tile_hex})")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

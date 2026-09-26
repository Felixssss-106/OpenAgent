"""Sweep every Windows page in both themes against its artboard by row bands.

Only the *positions* of matched bands are meaningful. Band counts are not comparable
between a design and a build: the build shows real data (a different number of tools,
providers, tasks, plugins) and a list whose rows are spaced differently merges into
fewer, taller bands. A count mismatch is a prompt to look, never a finding on its own.

Each area is compared against its own background, which is the mistake the first
version of this made: sampling the sidebar colour and using it as the reference for
the content column makes the whole column read as one band and reports "max 0px".

    python scripts/ui-band-sweep.py [prefix]
"""
import pathlib
import sys

import numpy as np
from PIL import Image

PAGES = {
    "agent": ("01", "02"),
    "tasks": ("13", "19"),
    "devices": ("14", "20"),
    "tools": ("15", "21"),
    "providers": ("16", "22"),
    "plugins": ("17", "23"),
    "settings": ("18", "24"),
}


def artboard(number):
    found = sorted(pathlib.Path("design/pixso-final").glob(f"{number}-*.png"))
    return found[0] if found else None


def bands(a, x0, x1, y0, y1, ref):
    out, cur = [], None
    for y in range(y0, y1):
        ink = int((np.abs(a[y, x0:x1] - ref).max(axis=1) > 12).sum())
        if ink >= 3:
            if cur and cur[1] == y - 1:
                cur[1] = y
            else:
                if cur:
                    out.append(tuple(cur))
                cur = [y, y]
    if cur:
        out.append(tuple(cur))
    return out


def compare(label, design_bands, build_bands):
    if len(design_bands) != len(build_bands):
        return f"{label:8s} {len(design_bands)} vs {len(build_bands)} bands"
    tops = max(abs(x[0] - y[0]) for x, y in zip(design_bands, build_bands))
    bots = max(abs(x[1] - y[1]) for x, y in zip(design_bands, build_bands))
    return f"{label:8s} {len(design_bands)} bands, top max {tops}px, bottom max {bots}px"


def main():
    prefix = sys.argv[1] if len(sys.argv) > 1 else "cur"

    for page, (light, dark) in PAGES.items():
        for theme, number in (("light", light), ("dark", dark)):
            design, build = artboard(number), pathlib.Path(f"artifacts/shots/{prefix}-{page}-{theme}.png")
            if design is None or not build.exists():
                print(f"{page:9s} {theme:5s} skipped")
                continue
            d = np.array(Image.open(design).convert("RGB")).astype(int)
            b = np.array(Image.open(build).convert("RGB")).astype(int)
            if d.shape != b.shape:
                print(f"{page:9s} {theme:5s} size {d.shape[:2]} vs {b.shape[:2]}")
                continue
            h, w = d.shape[:2]
            row = " | ".join(
                compare(
                    label,
                    bands(d, x0, x1, 44, h - 40, np.median(d[y0b:y1b, xb0:xb1].reshape(-1, 3), axis=0)),
                    bands(b, x0, x1, 44, h - 40, np.median(b[y0b:y1b, xb0:xb1].reshape(-1, 3), axis=0)),
                )
                for label, x0, x1, y0b, y1b, xb0, xb1 in (
                    ("sidebar", 12, 228, 100, h - 100, 6, 16),
                    ("content", 300, w - 40, 80, h - 80, w - 55, w - 35),
                )
            )
            print(f"{page:9s} {theme:5s} {row}")
    return 0


if __name__ == "__main__":
    sys.exit(main())

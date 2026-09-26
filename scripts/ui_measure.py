#!/usr/bin/env python3
"""Measure the geometry of a rendered window against a Pixso artboard.

Both are 1440x900 at 100% scaling, so every number below is directly comparable.
Run it on a design frame and a screenshot and diff the two columns:

    python scripts/ui-measure.py design/pixso-final/01-windows-起始页-浅色.png \\
                                 artifacts/shots/now-01-light.png
"""

import sys

from PIL import Image


def load(path):
    img = Image.open(path).convert("RGB")
    return img.load(), img.size


def close(a, b, tol):
    return all(abs(int(u) - int(v)) <= tol for u, v in zip(a, b))


def bands(px, x0, x1, y0, y1, ref, tol=12, min_ink=1):
    """Row ranges where something differs from `ref`, merged into bands."""
    out = []
    cur = None
    for y in range(y0, min(y1, 10000)):
        ink = sum(1 for x in range(x0, x1) if not close(px[x, y], ref, tol))
        if ink >= min_ink:
            if cur and cur[1] == y - 1:
                cur[1] = y
            else:
                if cur:
                    out.append(tuple(cur))
                cur = [y, y]
        else:
            if cur:
                out.append(tuple(cur))
                cur = None
    if cur:
        out.append(tuple(cur))
    return out


def hspan(px, y, x0, x1, ref, tol=12):
    xs = [x for x in range(x0, x1) if not close(px[x, y], ref, tol)]
    return (min(xs), max(xs)) if xs else None


def report(name, path):
    px, (w, h) = load(path)
    canvas = px[700, 300]
    sidebar = px[100, 300]
    out = {
        "file": name,
        "size": (w, h),
        "caption": px[700, 20],
        "sidebar_bg": sidebar,
        "canvas_bg": canvas,
        "sidebar_edge": hspan(px, 300, 200, 300, sidebar, 4),
        "sidebar_rows": bands(px, 12, 228, 44, 900, sidebar, 10),
        "content_bands": bands(px, 336, 1344, 44, 900, canvas, 14),
    }
    print(f"--- {name} ({path})")
    for k, v in out.items():
        print(f"  {k:14} {v}")
    return out


def main():
    if len(sys.argv) < 2:
        print(__doc__)
        return 2
    for path in sys.argv[1:]:
        report(path.split("/")[-1], path)
    return 0


if __name__ == "__main__":
    sys.exit(main())

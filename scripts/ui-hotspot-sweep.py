#!/usr/bin/env python3
"""Report structural disagreement between an artboard and the shipped build.

A DIAGNOSTIC, NOT A GATE: it always exits 0. Most of what it finds is the
mockup data deliberately not implemented (an empty device list where the frame
draws a paired one), which no threshold can tell apart from a real defect. Read
the output; do not assert on it. It is how the 44px row pitch and the missing
composer halo were found — both invisible to the gates that only look where
someone already thought to look.


A raw pixel diff is useless here: the mockup copy is deliberately not implemented,
so every text run lights up. This filters to pixels where BOTH images are locally
flat (a 5x5 window whose channels vary by <4) and still differ by more than 6
levels - i.e. surfaces, strokes, gaps and extents that disagree, not glyphs.

Clusters the survivors into rectangles and prints the biggest ones.
"""

import sys
from pathlib import Path

import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parent.parent
FLAT = 4
DIFF = 6
MIN_AREA = 400

PAIRS = [
    ("01-windows-起始页-浅色.png", "cur-agent-light.png"),
    ("02-windows-起始页-深色.png", "cur-agent-dark.png"),
    ("13-windows-任务-浅色.png", "cur-tasks-light.png"),
    ("19-windows-任务-深色.png", "cur-tasks-dark.png"),
    ("14-windows-设备-浅色.png", "cur-devices-light.png"),
    ("20-windows-设备-深色.png", "cur-devices-dark.png"),
    ("15-windows-工具-浅色.png", "cur-tools-light.png"),
    ("21-windows-工具-深色.png", "cur-tools-dark.png"),
    ("16-windows-Provider-浅色.png", "cur-providers-light.png"),
    ("22-windows-Provider-深色.png", "cur-providers-dark.png"),
    ("17-windows-插件-浅色.png", "cur-plugins-light.png"),
    ("23-windows-插件-深色.png", "cur-plugins-dark.png"),
    ("18-windows-设置-浅色.png", "cur-settings-light.png"),
    ("24-windows-设置-深色.png", "cur-settings-dark.png"),
]


def flat_mask(a):
    mn = a.copy()
    mx = a.copy()
    for dy in (-2, -1, 0, 1, 2):
        for dx in (-2, -1, 0, 1, 2):
            s = np.roll(np.roll(a, dy, axis=0), dx, axis=1)
            mn = np.minimum(mn, s)
            mx = np.maximum(mx, s)
    return (mx - mn).max(axis=2) < FLAT


def clusters(mask):
    """Greedy row-band clustering; enough to name a region, not to trace it."""
    ys = np.flatnonzero(mask.any(axis=1))
    out, cur = [], None
    for y in ys:
        if cur and y == cur[1] + 1:
            cur[1] = y
        else:
            if cur:
                out.append(cur)
            cur = [y, y]
    if cur:
        out.append(cur)
    boxes = []
    for y0, y1 in out:
        band = mask[y0:y1 + 1]
        xs = np.flatnonzero(band.any(axis=0))
        if not len(xs):
            continue
        x0, x1 = xs.min(), xs.max()
        area = int(band[:, x0:x1 + 1].sum())
        if area >= MIN_AREA:
            boxes.append((area, x0, x1, y0, y1))
    return sorted(boxes, reverse=True)


def main():
    for art, shot in PAIRS:
        ap = ROOT / "design" / "pixso-final" / art
        bp = ROOT / "artifacts" / "shots" / shot
        if not ap.exists() or not bp.exists():
            print(f"-- {art[:2]}: skipped (missing {shot})")
            continue
        a = np.asarray(Image.open(ap).convert("RGB")).astype(np.int16)
        b = np.asarray(Image.open(bp).convert("RGB")).astype(np.int16)
        if a.shape != b.shape:
            print(f"-- {art[:2]}: size {b.shape[:2]} vs {a.shape[:2]}")
            continue
        both_flat = flat_mask(a) & flat_mask(b)
        diff = np.abs(a - b).max(axis=2)
        mask = both_flat & (diff > DIFF)
        boxes = clusters(mask)
        print(f"-- {art[:2]} vs {shot}: {int(mask.sum())} structural pixels")
        for area, x0, x1, y0, y1 in boxes[:6]:
            sa = tuple(int(v) for v in a[(y0 + y1) // 2, (x0 + x1) // 2])
            sb = tuple(int(v) for v in b[(y0 + y1) // 2, (x0 + x1) // 2])
            print(f"     {area:6d}px  x {x0:4d}..{x1:4d}  y {y0:4d}..{y1:4d}  art{sa} build{sb}")
        sys.stdout.flush()


if __name__ == "__main__":
    main()

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

# The phone's AVD is 1080x2400 (390x866 dp) and the artboards are 390x844, so a whole-frame
# diff has no common origin. Both do draw the tab bar's top hairline at a known
# colour, so the frames are registered on that and compared over the band around
# it — the composer, the tab bar and the content above them.
PHONE_PAIRS = [
    ("07-android-起始页-浅色.png", "raw-agent-light.png", (255, 255, 255)),
    ("08-android-起始页-深色.png", "raw-agent-dark.png", (10, 10, 10)),
    ("25-android-任务-浅色.png", "raw-tasks-light.png", (255, 255, 255)),
    ("28-android-任务-深色.png", "raw-tasks-dark.png", (10, 10, 10)),
    ("26-android-设备-浅色.png", "raw-devices-light.png", (255, 255, 255)),
    ("29-android-设备-深色.png", "raw-devices-dark.png", (10, 10, 10)),
    ("27-android-设置-浅色.png", "raw-settings-light.png", (255, 255, 255)),
    ("30-android-设置-深色.png", "raw-settings-dark.png", (10, 10, 10)),
]
HAIRLINE = {(255, 255, 255): (229, 229, 234), (10, 10, 10): (44, 44, 46)}
# Only the band around the registered landmark is honestly comparable: the tab
# bar and composer hang off the bottom of the window while the scrolling content
# above them is anchored to the top, and the AVD is 866dp tall against the
# artboard's 844. Widening this to cover the content just measures that 22dp.
BAND = 120


def flat_mask(a):
    mn = a.copy()
    mx = a.copy()
    for dy in (-2, -1, 0, 1, 2):
        for dx in (-2, -1, 0, 1, 2):
            s = np.roll(np.roll(a, dy, axis=0), dx, axis=1)
            mn = np.minimum(mn, s)
            mx = np.maximum(mx, s)
    return (mx - mn).max(axis=2) < FLAT


def mode_of(arr, y0, y1, x0, x1):
    """The most common exact colour inside a cluster's box."""
    box = arr[y0:y1 + 1, x0:x1 + 1].reshape(-1, 3).astype(np.int64)
    packed = box[:, 0] * 65536 + box[:, 1] * 256 + box[:, 2]
    values, counts = np.unique(packed, return_counts=True)
    v = int(values[counts.argmax()])
    return (v // 65536, v // 256 % 256, v % 256)


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


def report(label, shot, a, b, y0=0, y1=None):
    """Print the flat-but-different clusters in one window of two aligned frames."""
    y1 = a.shape[0] if y1 is None else min(y1, a.shape[0], b.shape[0])
    sa, sb = a[y0:y1], b[y0:y1]
    if sa.shape != sb.shape:
        print(f"-- {label}: window {sa.shape} vs {sb.shape}")
        return
    mask = flat_mask(sa) & flat_mask(sb) & (np.abs(sa - sb).max(axis=2) > DIFF)
    print(f"-- {label} vs {shot}: {int(mask.sum())} structural pixels")
    for area, x0, x1, by0, by1 in clusters(mask)[:5]:
        # The mode of each side, not the pixel at the centre of the bounding box:
        # a one-step surface error (the phone's status pill drew bgSurface where the
        # artboard draws bgSunken) hides at a centre that may be anything, but the
        # dominant colour of a few thousand flat pixels is unambiguous.
        ca, cb = mode_of(sa, by0, by1, x0, x1), mode_of(sb, by0, by1, x0, x1)
        print(f"     {area:6d}px  x {x0:4d}..{x1:4d}  y {by0 + y0:4d}..{by1 + y0:4d}"
              f"  art{ca} build{cb}")
    sys.stdout.flush()


def tabbar_top(px, hair, h):
    """The y of the tab bar's top hairline: the lowest full-width row of that colour.
    Tolerant because resampling a 3-device-pixel line to dp blends it; exact matching
    only ever worked on the artboards, which are already drawn at dp."""
    for y in range(h - 1, int(h * 0.5), -1):
        if all(max(abs(int(u) - int(v)) for u, v in zip(px[x, y], hair)) <= 6
               for x in range(60, 330, 10)):
            return y
    return None


def phone_dp(path):
    """Raw device pixels, rescaled to dp *without* squashing the extra height."""
    im = Image.open(path).convert("RGB")
    if im.size[0] != 390:
        im = im.resize((390, round(im.size[1] * 390 / im.size[0])))
    return np.asarray(im).astype(np.int16)


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
        report(art[:2], shot, a, b)

    for art, shot, canvas in PHONE_PAIRS:
        ap, bp = ROOT / "design" / "pixso-final" / art, ROOT / "artifacts" / "shots" / shot
        if not ap.exists() or not bp.exists():
            print(f"-- {art[:2]}: skipped (missing {shot})")
            continue
        a, b = phone_dp(ap), phone_dp(bp)
        hair = HAIRLINE[canvas]
        ya = tabbar_top(Image.fromarray(a.astype(np.uint8)).load(), hair, a.shape[0])
        yb = tabbar_top(Image.fromarray(b.astype(np.uint8)).load(), hair, b.shape[0])
        if ya is None or yb is None:
            print(f"-- {art[:2]}: no {hair} tab-bar hairline (art {ya}, build {yb})")
            continue
        # Register on the hairline, then compare BAND rows above and below it.
        top = max(ya, yb) - BAND
        lo_a, lo_b = max(0, top - ya), max(0, top - yb)
        note = "" if ya == yb else f" [frame differs by {yb - ya}dp; see BAND]"
        report(f"{art[:2]} (tabbar art{ya} build{yb})", shot, a[lo_a:], b[lo_b:])
        if note:
            print(f"     note{note}")


if __name__ == "__main__":
    main()

#!/usr/bin/env python3
"""Audit the phone build against its artboards, and fail when it drifts.

Three properties the design states and the code previously got wrong:

  * content gutter  -- cards sit 20dp from the edge; the build shipped 24
  * card hairline   -- a 1px border-subtle line on the card boundary
  * accent          -- the same accent blue appears in the frame and in the build

Resolution matters, so the rules are explicit:

  * Geometry is compared at the artboard's 390dp scale, on any capture.
  * The hairline is only checked on a **raw device capture** (width >= 1000). A 1px
    line down-scaled from 1080 to 390 blends to roughly 242, and an earlier version of
    this script "passed" on that blend -- a false green. Non-raw inputs are skipped
    out loud instead.
  * Accent is a presence test, per side, scaled by each file's own resolution.

    python scripts/ui-android-audit.py

Exit 0 only when every applicable check passes.
"""
import pathlib
import sys

import numpy as np
from PIL import Image

ACCENT = {"light": (0, 122, 255), "dark": (46, 141, 255)}
HAIR = {"light": (229, 229, 234), "dark": (44, 44, 46)}
FILL = {"light": (247, 247, 250), "dark": (28, 28, 30)}
RAW_MIN_WIDTH = 1000
PAGE_GUTTER_MIN = 8

# artboard, capture, theme
CASES = [
    ("27", "artifacts/shots/raw-settings-light.png", "light"),
    ("30", "artifacts/shots/raw-settings-dark.png", "dark"),
    ("26", "artifacts/shots/raw-devices-light.png", "light"),
    ("29", "artifacts/shots/raw-devices-dark.png", "dark"),
    ("07", "artifacts/shots/raw-agent-light.png", "light"),
    ("08", "artifacts/shots/raw-agent-dark.png", "dark"),
]


def to_dp(path):
    im = Image.open(path).convert("RGB")
    if im.size[0] != 390:
        im = im.resize((390, int(im.size[1] * 390 / im.size[0])), Image.LANCZOS)
    return np.array(im).astype(int)


def card_edge(a):
    for theme in FILL:
        hit = (np.abs(a - np.array(FILL[theme])).max(axis=2) <= 6)
        lefts = [int(np.where(hit[y])[0].min()) for y in range(120, min(600, a.shape[0]))
                 if len(np.where(hit[y])[0]) > 150]
        if lefts and min(lefts) >= PAGE_GUTTER_MIN:
            return min(lefts), theme
    return None, None


def native_hairline(path, theme):
    a = np.array(Image.open(path).convert("RGB")).astype(int)
    step = a.shape[1] / 390
    want = np.array(HAIR[theme])
    hit = (np.abs(a - np.array(FILL[theme])).max(axis=2) <= 6)
    for y in range(int(150 * step), int(620 * step)):
        run = np.where(hit[y])[0]
        if len(run) > 150 * step:
            x = int(run.min())
            for i in range(max(x - 4, 0), x + 5):
                if int(np.abs(a[y, i] - want).max()) <= 14:
                    return True, tuple(int(v) for v in a[y, i])
    return False, None


def accent_count(a, theme):
    return int((np.abs(a - np.array(ACCENT[theme])).max(axis=2) <= 26).sum())


def main():
    failed = 0
    for number, build, theme in CASES:
        design_p = sorted(pathlib.Path("design/pixso-final").glob(f"{number}-*.png"))
        build_p = pathlib.Path(build)
        print(f"--- artboard {number} vs {build_p.name} ({theme}) ---")
        if not design_p:
            print(f"  FAIL  no artboard {number}")
            failed += 1
            continue
        if not build_p.exists():
            print(f"  FAIL  missing capture {build}")
            failed += 1
            continue

        d, b = to_dp(design_p[0]), to_dp(build)
        dx, _ = card_edge(d)
        bx, _ = card_edge(b)
        if dx is None or bx is None:
            print(f"  --    no cards on this page (design {dx}, build {bx})")
        else:
            ok = abs(dx - bx) <= 2
            failed += 0 if ok else 1
            print(f"  {'ok  ' if ok else 'FAIL'}  gutter: design {dx}dp build {bx}dp")

        if Image.open(build).size[0] < RAW_MIN_WIDTH:
            print(f"  --    hairline skipped: {build} is {Image.open(build).size[0]}px wide, not raw")
        elif bx is None:
            # Nothing to measure: this page draws no card in the build (an empty devices
            # or start screen). The design may have cards, so the gate keys off the build.
            print("  --    hairline skipped: this page draws no card in the build")
        else:
            ok, px = native_hairline(build, theme)
            failed += 0 if ok else 1
            print(f"  {'ok  ' if ok else 'FAIL'}  card hairline at native scale: {px} "
                  f"(want near {HAIR[theme]})")

        dp = accent_count(d, theme)
        bp = accent_count(b, theme)
        if dp < 30:
            print(f"  --    artboard has no accent here ({dp}px); skipping")
        else:
            ok = bp >= 30
            failed += 0 if ok else 1
            print(f"  {'ok  ' if ok else 'FAIL'}  accent {ACCENT[theme]}: design {dp}px build {bp}px")
        print()

    print(f"failures: {failed}")
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())

"""Assert a driven-state capture actually shows the driven state.

The screenshot pipeline can succeed at every step and still shoot the wrong thing: a
SendKeys call that failed to take foreground leaves the app on its start page, the PNG is
genuinely new, and the mtime guard passes. So compare against the pristine start-page
capture of the same theme and require the content column to have changed, and for the
approval state require the card's orange border to exist at all.

    python scripts/ui-state-check.py approval light
"""
import sys

import numpy as np
from PIL import Image

CONTENT_X = 300
TOLERANCE = 6

# Fixed points that exist in the artboard and the capture at the same coordinates: the
# content background, the sidebar background, and the approval card's own fill. The card
# is only sampled for the approval state.
LANDMARKS = (("content bg", 400, 500), ("sidebar bg", 200, 500), ("card fill", 900, 700))


def accent_median(a):
    blue = (a[:, :, 2] > 180) & (a[:, :, 0] < 120) & (a[:, :, 1] > 60) & (a[:, :, 1] < 190)
    ys, xs = np.nonzero(blue)
    if not len(ys):
        return None
    return np.median(a[ys, xs], axis=0)


def compare_colours(design, build, state):
    """Return the worst landmark delta, failing above TOLERANCE."""
    worst = 0
    for label, x, y in LANDMARKS:
        if label == "card fill" and state != "approval":
            continue
        d = np.abs(design[y, x] - build[y, x]).max()
        worst = max(int(worst), int(d))
        if d > TOLERANCE:
            print(f"FAILED: {label} drifted {d}: design {tuple(design[y, x])} build {tuple(build[y, x])}")
    a, b = accent_median(design), accent_median(build)
    if a is None or b is None:
        print(f"FAILED: accent blue missing (design {a is not None}, build {b is not None})")
        return TOLERANCE + 1
    d = int(np.abs(a - b).max())
    worst = max(worst, d)
    if d > TOLERANCE:
        print(f"FAILED: accent drifted {d}: design {tuple(int(v) for v in a)} build {tuple(int(v) for v in b)}")
    print(f"  landmarks worst delta {worst}px-scale (tolerance {TOLERANCE})")
    return worst


def card_geometry(a):
    """(left, right, top, bottom) of the approval card's orange border, or None."""
    orange = (a[:, :, 0] > 200) & (a[:, :, 1] > 100) & (a[:, :, 1] < 190) & (a[:, :, 2] < 90)
    orange = orange[:, 300:1400]
    ys, xs = np.nonzero(orange)
    if not len(ys):
        return None
    return int(xs.min()) + 300, int(xs.max()) + 300, int(ys.min()), int(ys.max())


def approve_button(a):
    """(left, top, width, height) of the filled accent button."""
    blue = (a[:, :, 2] > 170) & (a[:, :, 0] < 130) & (a[:, :, 1] > 50) & (a[:, :, 1] < 200)
    blue[0:600] = False
    ys, xs = np.nonzero(blue)
    if not len(ys):
        return None
    return int(xs.min()), int(ys.min()), int(xs.max()) - int(xs.min()) + 1, int(ys.max()) - int(ys.min()) + 1


GEOMETRY_TOLERANCE = 3

# What is asserted and what is only reported, and why. The card's horizontal extents, its
# bottom edge and the button's row are placed by the shell, so the artboard and the build
# agree to the pixel and any drift is a real change. The card's *top* is not asserted:
# the card is bottom-anchored and grows with its content, and the content's glyphs ink a
# few rows taller for some strings ("启动 记事本" vs "移动 35 个文件"), which moves the top
# edge by 8px without anything in the layout having changed.
SUNKEN = {"light": (242, 242, 247), "dark": (0, 0, 0)}


def sunken_block(a, theme):
    """The tool-output panel's horizontal extents, or None when it is not drawn.

    Its width is set by the content column, so it is layout and worth asserting; its
    height and top follow the text the tool returned, which is data — the shipped build
    answers with real system information, the artboard with a sample listing.
    """
    want = np.array(SUNKEN[theme])
    hit = (np.abs(a - want).max(axis=2) <= 4)
    hit[:, :330] = False
    hit[:200, :] = False
    hit[700:, :] = False
    left = right = None
    rows = 0
    for y in range(200, 700):
        run = np.where(hit[y])[0]
        if len(run) > 400:
            rows += 1
            left = int(run.min()) if left is None else min(left, int(run.min()))
            right = int(run.max()) if right is None else max(right, int(run.max()))
    return None if left is None else (left, right, rows)


def compare_geometry(design, build, state, theme):
    """Fail on chrome the artboard places and the build places elsewhere."""
    worst = 0
    if state == "chat":
        ds, bs = sunken_block(design, theme), sunken_block(build, theme)
        if ds is None or bs is None:
            print(f"FAILED: tool output panel missing (artboard {ds}, build {bs})")
            return 1
        for name, d, b in (("panel left", ds[0], bs[0]), ("panel right", ds[1], bs[1])):
            worst = max(worst, abs(d - b))
            if abs(d - b) > GEOMETRY_TOLERANCE:
                print(f"FAILED: {name} at {b}, artboard draws {d}")
        print(f"  geometry: output panel x {bs[0]}..{bs[1]} "
              f"(worst delta {worst}px; height {ds[2]} vs {bs[2]} rows is content)")
        return 1 if worst > GEOMETRY_TOLERANCE else 0

    dg, bg = card_geometry(design), card_geometry(build)
    if dg is None or bg is None:
        print(f"FAILED: approval card border missing (design {dg}, build {bg})")
        return 1
    for name, d, b in (("card left", dg[0], bg[0]), ("card right", dg[1], bg[1]),
                       ("card bottom", dg[3], bg[3])):
        worst = max(worst, abs(d - b))
        if abs(d - b) > GEOMETRY_TOLERANCE:
            print(f"FAILED: {name} at {b}, artboard draws {d}")
    db, bb = approve_button(design), approve_button(build)
    if db is None or bb is None:
        print(f"FAILED: accent button missing (design {db}, build {bb})")
        return max(worst, 1)
    for name, d, b in (("button left", db[0], bb[0]), ("button top", db[1], bb[1]),
                       ("button height", db[3], bb[3])):
        worst = max(worst, abs(d - b))
        if abs(d - b) > GEOMETRY_TOLERANCE:
            print(f"FAILED: {name} at {b}, artboard draws {d}")
    print(f"  geometry: card {bg[0]}..{bg[1]} bottom {bg[3]}, button {bb[0]},{bb[1]} h{bb[3]} "
          f"(worst delta {worst}px; card top {dg[2]} vs {bg[2]} reported only)")
    return 1 if worst > GEOMETRY_TOLERANCE else 0


def check(state, theme):
    shot = f"artifacts/shots/state-{state}-{theme}.png"
    base = f"artifacts/shots/cur-agent-{theme}.png"
    try:
        a = np.array(Image.open(shot).convert("RGB")).astype(int)
        b = np.array(Image.open(base).convert("RGB")).astype(int)
    except FileNotFoundError as ex:
        print(f"FAILED: {ex}")
        return 1
    if a.shape != b.shape:
        print(f"FAILED: {shot} is {a.shape[:2]} but {base} is {b.shape[:2]}")
        return 1

    diff = (np.abs(a[:, CONTENT_X:] - b[:, CONTENT_X:]).max(axis=2) > 40)
    changed = float(diff.sum()) / diff.size
    if changed < 0.002:
        print(f"FAILED: {shot} matches the start page ({changed:.2%} of the content column changed)")
        return 1
    print(f"{state}/{theme}: content column differs from the start page by {changed:.2%}")

    if state == "approval":
        orange = (a[:, :, 0] > 200) & (a[:, :, 1] > 100) & (a[:, :, 1] < 190) & (a[:, :, 2] < 90)
        rows = int((orange[:, CONTENT_X:1100].sum(axis=1) > 2).sum())
        if rows < 4:
            print(f"FAILED: no approval card border in {shot} (orange rows {rows})")
            return 1
        print(f"{state}/{theme}: approval card border spans {rows} rows")

    import glob
    import pathlib
    number = {"approval": {"light": "05", "dark": "06"}, "chat": {"light": "03", "dark": "04"}}[state][theme]
    found = sorted(pathlib.Path("design/pixso-final").glob(f"{number}-*.png"))
    if not found:
        print(f"FAILED: no artboard {number} to compare against")
        return 1
    art = np.array(Image.open(found[0]).convert("RGB")).astype(int)
    if art.shape != a.shape:
        print(f"FAILED: artboard {number} is {art.shape[:2]}, capture is {a.shape[:2]}")
        return 1
    colour = compare_colours(art, a, state)
    geometry = compare_geometry(art, a, state, theme)
    return 1 if (colour > TOLERANCE or geometry) else 0


if __name__ == "__main__":
    if len(sys.argv) != 3:
        print(__doc__)
        sys.exit(2)
    sys.exit(check(sys.argv[1], sys.argv[2]))

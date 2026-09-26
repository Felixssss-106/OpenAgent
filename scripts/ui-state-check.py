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
    return 1 if compare_colours(art, a, state) > TOLERANCE else 0


if __name__ == "__main__":
    if len(sys.argv) != 3:
        print(__doc__)
        sys.exit(2)
    sys.exit(check(sys.argv[1], sys.argv[2]))

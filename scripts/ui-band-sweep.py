"""Sweep every Windows page in both themes against its artboard by row bands.

Only the *positions* of matched bands are meaningful. Band counts are not comparable
between a design and a build: the build shows real data (a different number of tools,
providers, tasks, plugins) and a list whose rows are spaced differently merges into
fewer, taller bands. A count mismatch is a prompt to look, never a finding on its own.

Each area is compared against its own background, which is the mistake the first
version of this made: sampling the sidebar colour and using it as the reference for
the content column makes the whole column read as one band and reports "max 0px".

    python scripts/ui-band-sweep.py [prefix]      # report
    python scripts/ui-band-sweep.py --gate        # assert the sidebar chrome

The gate covers the sidebar chrome only — the search field, the nav block, the divider
and the account row at the foot. That is the part of every page that is drawn by the
shell and therefore has one correct answer; the content column below it is real data.

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


# The sidebar chrome in two windows: the search field and nav block at the head, the
# divider, 设置 and the account row at the foot. Both are drawn by the shell, so there
# is one right answer for every page, unlike the content column.
CHROME = (("head", 44, 200), ("foot", None, 20))

# A band the artboard draws must have a build band starting within this many pixels.
# The worst drift measured across all 14 pairs is 3px, and it is not layout: the same
# row of the same label, drawn at the same height in a row spaced identically to the
# artboard (design tops 319/348 apart, build 317/346 apart), simply inks 2px higher.
# That is text metrics, which no margin constant can move. 4 leaves headroom over the
# observed worst so the gate does not sit on its own edge, while still catching a real
# change to the sidebar's spacing or an element's height.
CHROME_TOLERANCE = 4


def sidebar_bands(a, h, y0, y1):
    ref = np.median(a[100:h - 100, 6:16].reshape(-1, 3), axis=0)
    return bands(a, 12, 228, y0, y1, ref)


def chrome_gate(d, b, h, page, theme):
    """Fail on chrome the artboard draws and the build does not draw, or draws elsewhere.

    Only band tops are asserted. A band's bottom is the last row its glyphs ink, so it
    moves with the typeface: the artboard's nav label inks to y=215 and the build's to
    y=211 because of the descender on a different font, not because anything moved. The
    same rasterisation splits one artboard band into two build bands (a 1px gap inside a
    text row), which is why extras are counted rather than matched.

    Extra bands in the build do not fail, for two measured reasons: the 30 frames
    disagree with each other about the hairline above the account row (artboard 01 inks
    it at y=759, 13/14/15/16/17/18 do not, and the shell draws it on every page), and the
    nav list is data — the build shows the tasks that actually exist. Bending the shell
    to match one frame's oversight, or failing it for showing real data, would make the
    gate worse than useless.
    """
    failures, notes, worst = [], [], [0, 0]
    for label, start, stop in CHROME:
        y0 = start if start is not None else h - 160
        y1 = h - stop
        design = sidebar_bands(d, h, y0, y1)
        build = sidebar_bands(b, h, y0, y1)
        for top, bot in design:
            near = [x for x in build if abs(x[0] - top) <= CHROME_TOLERANCE]
            if not near:
                failures.append(f"{page}/{theme} {label}: artboard band y={top}..{bot} not drawn")
                continue
            best = min(near, key=lambda x: abs(x[0] - top))
            worst[0] = max(worst[0], abs(best[0] - top))
            worst[1] = max(worst[1], abs(best[1] - bot))
        extra = [x for x in build if not any(abs(x[0] - t) <= CHROME_TOLERANCE for t, _ in design)]
        if extra:
            notes.append(f"{page}/{theme} {label}: {len(extra)} build-only band(s) {extra}")
    return failures, notes, worst


def main():
    args = [a for a in sys.argv[1:]]
    gate = "--gate" in args
    args = [a for a in args if not a.startswith("--")]
    prefix = args[0] if args else "cur"

    failures, notes, worst = [], [], [0, 0]
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
            if gate:
                bad, extra, pair = chrome_gate(d, b, h, page, theme)
                failures += bad
                notes += extra
                worst = [max(worst[0], pair[0]), max(worst[1], pair[1])]
                print(f"{page:9s} {theme:5s} chrome {len(bad)} problem(s), "
                      f"top {pair[0]}px, height {pair[1]}px")
                continue
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

    if gate:
        for line in notes:
            print("note  " + line)
        for line in failures:
            print("FAIL  " + line)
        print(f"chrome gate: {len(failures)} failure(s), {len(notes)} build-only note(s), "
              f"worst top {worst[0]}px / height {worst[1]}px (tolerance {CHROME_TOLERANCE})")
        return 1 if failures else 0
    return 0


if __name__ == "__main__":
    sys.exit(main())

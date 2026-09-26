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


# The windows whose rows the shell places rather than the data: the sidebar's head
# (search field, nav) and foot (divider, 设置, account), and the page header in the
# content column (title, subtitle, hairline).
#
# Each entry is (label, x0, x1, refx0, refx1, y0, y1); an x1 of None means "window
# width minus 120", because the content column's scrollbar sits further right and its
# track is inked on every row — including it merges the whole header into one band and
# the gate reports nothing. A y0 of None means "160 from the bottom".
GATES = (
    ("head", 12, 228, 6, 16, 44, 460),
    ("foot", 12, 228, 6, 16, None, 20),
    ("header", 360, -160, 300, 316, 44, 205),
)

# A band the artboard draws must have a build band starting within this many pixels.
# The worst drift measured across all 14 pairs is 4px and it is not layout: the devices
# subtitle now reads "1 台设备 · 0 台在线" where the artboard draws "2 台已配对 · 1 台在线",
# and different words ink their first row a couple of pixels apart. The smallest real
# defect this method has caught is 14px (the heading-to-card gap on the tools and settings
# pages), so 6 sits between the typeface noise and any margin that actually moved.
CHROME_TOLERANCE = 6


def window_bands(a, w, x0, x1, refx0, refx1, y0, y1):
    right = w + x1 if isinstance(x1, int) and x1 < 0 else (w - 120 if x1 is None else x1)
    ref = np.median(a[y0:y1, refx0:refx1].reshape(-1, 3), axis=0)
    return bands(a, x0, right, y0, y1, ref)


DIVIDER = {"light": (229, 229, 234), "dark": (44, 44, 46)}

# The divider under the page title spans the content column, so its left and right ends
# are set by the shell rather than by the data below it — the one measurement that catches
# a content-column width change. Tolerance 4, not 3: the settings page's rounded card
# corners land on a different row of the arc and read up to 3px wide.
DIVIDER_TOLERANCE = 4


def divider_span(a, theme, y0=150, y1=210):
    want = np.array(DIVIDER[theme])
    for y in range(y0, y1):
        hit = np.where(np.abs(a[y, 320:1420] - want).max(axis=1) <= 8)[0]
        if len(hit) > 600:
            return int(hit.min()) + 320, int(hit.max()) + 320
    return None


def compare_divider(design, build, theme, page, kind):
    """Fail when the artboard draws a divider the build does not, or draws it wider."""
    d, b = divider_span(design, theme), divider_span(build, theme)
    if d is None and b is None:
        return [], f"{page}/{theme} {kind}: no divider on this page"
    if d is None:
        return [], f"{page}/{theme} {kind}: build draws a divider the artboard does not"
    if b is None:
        return [f"{page}/{theme} {kind}: artboard divider at x {d[0]}..{d[1]} not drawn"], ""
    bad = []
    for name, want, got in (("left", d[0], b[0]), ("right", d[1], b[1])):
        if abs(want - got) > DIVIDER_TOLERANCE:
            bad.append(f"{page}/{theme} {kind}: divider {name} at {got}, artboard draws {want}")
    note = "" if bad else f"{page}/{theme} {kind}: divider x {b[0]}..{b[1]}"
    return bad, note


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
    w = d.shape[1]
    for label, x0, x1, refx0, refx1, top, bottom in GATES:
        y0 = (h - 160) if top is None else top
        y1 = (h - bottom) if top is None else bottom
        design = window_bands(d, w, x0, x1, refx0, refx1, y0, y1)
        build = window_bands(b, w, x0, x1, refx0, refx1, y0, y1)
        for t, bot in design:
            near = [x for x in build if abs(x[0] - t) <= CHROME_TOLERANCE]
            if not near:
                failures.append(f"{page}/{theme} {label}: artboard band y={t}..{bot} not drawn")
                continue
            best = min(near, key=lambda x: abs(x[0] - t))
            worst[0] = max(worst[0], abs(best[0] - t))
            worst[1] = max(worst[1], abs(best[1] - bot))
        extra = [x for x in build if not any(abs(x[0] - t) <= CHROME_TOLERANCE for t, _ in design)]
        if extra:
            notes.append(f"{page}/{theme} {label}: {len(extra)} build-only band(s) {extra}")

    bad, note = compare_divider(d, b, theme, page, "divider")
    failures += bad
    if note:
        notes.append(note)
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

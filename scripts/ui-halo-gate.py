#!/usr/bin/env python3
"""Gate the composer's halo against the artboards, for both themes.

The halo is the one place the shipped UI paints a soft shadow, and it is painted
as ten stacked capsules (see ComposerHalo in AgentPage.xaml) because WinUI 3
offers no shadow mount point. Nothing else in the audit suite looks at pixels
*outside* a shape's border, so a regression that drops the stack — or drops one
theme's share of it — would pass every other gate silently.

Each profile is the darkening measured one pixel step at a time away from the
pill's border. The tolerance is 2 levels of 255, which is as good as a stack of
rounded rectangles can do: the dark canvas sits at 10, so the whole shadow there
occupies three quantisation steps, and WinUI rounds the channel after every
layer it composites.

    python scripts/ui-halo-gate.py [shots-dir]
"""

import sys
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parent.parent
SHOTS = Path(sys.argv[1]) if len(sys.argv) > 1 else ROOT / "artifacts" / "shots"

# (artboard, capture, pill surface fill, canvas brightness)
CASES = [
    ("01-windows-起始页-浅色.png", "cur-agent-light.png", (247, 247, 250), 255),
    ("02-windows-起始页-深色.png", "cur-agent-dark.png", (28, 28, 30), 10),
]

TOLERANCE = 2
# Below this the artboard is not drawing anything measurable, so there is no
# target to hit — three levels on a dark canvas is the whole shadow there.
MIN_ARTBOARD_PEAK = 1


def load(path):
    return Image.open(path).convert("RGB").load()


def readings(px, fill, canvas):
    top = min(y for y in range(700, 899) if px[800, y] == fill)
    bottom = max(y for y in range(700, 899) if px[800, y] == fill)
    mid = (top + bottom) // 2
    right = max(x for x in range(370, 1420) if px[x, mid] == fill)
    return {
        "below": [canvas - px[800, y][0] for y in range(bottom + 2, min(bottom + 22, 900))],
        "above": [canvas - px[800, y][0] for y in range(max(top - 9, 0), top - 1)],
        "right": [canvas - px[x, mid][0] for x in range(right + 3, min(right + 18, 1440))],
    }


def main():
    bad = 0
    for art_name, shot_name, fill, canvas in CASES:
        if not (ROOT / "design" / "pixso-final" / art_name).exists():
            print(f"MISSING {art_name}")
            bad += 1
            continue
        shot = SHOTS / shot_name
        if not shot.exists():
            print(f"MISSING {shot_name} — run scripts/ui-shot.ps1 first")
            bad += 1
            continue

        want = readings(load(ROOT / "design" / "pixso-final" / art_name), fill, canvas)
        got = readings(load(shot), fill, canvas)
        for edge in ("below", "above", "right"):
            peak, built = max(want[edge]), max(got[edge])
            if peak < MIN_ARTBOARD_PEAK:
                print(f"SKIP {shot_name} {edge}: artboard peaks at {peak}, nothing measurable to gate")
                continue
            worst = max(abs(a - b) for a, b in zip(want[edge], got[edge]))
            overs = [f"{i}px want {a} +/-{TOLERANCE} got {b}"
                     for i, (a, b) in enumerate(zip(want[edge], got[edge]), 1)
                     if abs(a - b) > TOLERANCE]
            if overs:
                print(f"FAIL {shot_name} {edge}: " + ", ".join(overs))
                bad += 1
            else:
                print(f"ok   {shot_name} {edge}: peak {built} vs {peak}, worst {worst} levels")
    return 1 if bad else 0


if __name__ == "__main__":
    sys.exit(main())

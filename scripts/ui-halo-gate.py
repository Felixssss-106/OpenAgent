#!/usr/bin/env python3
"""Gate the composer's halo — the app's only soft shadow — on both platforms.

Nothing else in the audit suite looks at pixels *outside* a shape's border, so a
regression that drops the halo would pass every other gate silently. Each profile
is the darkening measured one dp at a time away from the capsule's border.

Windows paints it as ten stacked capsules (see ComposerHalo in AgentPage.xaml)
because WinUI 3 offers no shadow mount point at all. Android has a real one and
uses `Modifier.shadow`, whose Gaussian cannot reproduce the design's near-linear
ramp: the head matches to 2 levels, the tail reads 2-3 levels where the artboard
reaches 5-8. The tolerance below is set to what both platforms can actually hit,
not to a round number.

    python scripts/ui-halo-gate.py [shots-dir]
"""

import sys
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parent.parent
SHOTS = Path(sys.argv[1]) if len(sys.argv) > 1 else ROOT / "artifacts" / "shots"

# Windows reaches both edges exactly; Android's Gaussian is offset downward, so
# it cannot lift enough ink above the capsule. The tolerances are what each
# mechanism achieves, measured, not round numbers.
WINDOWS_TOLERANCE = 2
ANDROID_TOLERANCE = 3
# Below this the artboard is not drawing anything measurable, so there is no
# target to hit — three levels on a dark canvas is the whole shadow there.
MIN_ARTBOARD_PEAK = 1
# 12, not more: the phone's composer sits 12dp above the tab bar, and a 13th
# sample lands on that widget's own border and compares two different things.
RAMP_DP = 12

# (artboard, capture, pill surface fill, canvas brightness). The Windows pairs are
# 1440x900 window captures at 100% scaling, so one pixel is one dp. The chat state
# is driven by scripts/ui-state-verify.sh, and the composer floats there too — the
# same capsule has to carry the same halo in both states.
WINDOWS_CASES = [
    ("01-windows-起始页-浅色.png", "cur-agent-light.png", (247, 247, 250), 255),
    ("02-windows-起始页-深色.png", "cur-agent-dark.png", (28, 28, 30), 10),
    ("03-windows-对话态-浅色.png", "state-chat-light.png", (247, 247, 250), 255),
    ("04-windows-对话态-深色.png", "state-chat-dark.png", (28, 28, 30), 10),
]

# The phone artboards are drawn at dp scale but captured at 420dpi, so its ramp
# is stepped in device pixels. `border` is the hairline the capsule must carry:
# borderDefault on the composer, borderSubtle on the tab bar.
PHONE_CASES = [
    ("07-android-起始页-浅色.png", "raw-agent-light.png", (247, 247, 250), 255,
     (209, 209, 214), (229, 229, 234)),
    ("08-android-起始页-深色.png", "raw-agent-dark.png", (28, 28, 30), 10,
     (58, 58, 60), (44, 44, 46)),
]
PHONE_DENSITY = 2.625  # 1080 device px / 411 dp on the AVD these came from


def load(path):
    img = Image.open(path).convert("RGB")
    return img.load(), img.size


def windows_readings(px, fill, canvas):
    top = min(y for y in range(700, 899) if px[800, y] == fill)
    bottom = max(y for y in range(700, 899) if px[800, y] == fill)
    mid = (top + bottom) // 2
    right = max(x for x in range(370, 1420) if px[x, mid] == fill)
    return {
        "below": [canvas - px[800, y][0] for y in range(bottom + 2, min(bottom + 22, 900))],
        "above": [canvas - px[800, y][0] for y in range(max(top - 9, 0), top - 1)],
        "right": [canvas - px[x, mid][0] for x in range(right + 3, min(right + 18, 1440))],
    }


def capsule(px, size, fill, column):
    """The composer capsule on the phone: the *uppermost* run of `fill` in the
    lower third. The tab bar below it is a longer run of the same colour, so
    picking the longest one silently measures the wrong widget."""
    _, h = size
    cur = None
    for y in range(int(h * 0.6), h):
        if px[column, y] == fill:
            cur = y if cur is None else cur
        elif cur is not None:
            if y - cur > 30:
                return cur, y - 1
            cur = None
    raise AssertionError("no capsule of that fill in the lower third")


def phone_readings(px, size, fill, canvas, density, tab):
    w, _ = size
    column = w // 2
    top, bottom = capsule(px, size, fill, column)
    border = round(density)
    start = bottom + 1 + border           # first pixel past the capsule's own stroke
    above = top - 1 - border              # last pixel before it
    ramp = lambda base, sign: [
        canvas - px[column, base + sign * round(k * density)][0] for k in range(RAMP_DP)
    ]
    # Tolerance 2, not 6: the composer's own halo decays through 235 one pixel after
    # its border, and the light hairline is 229 — a loose match lands on the halo and
    # reports a zero gap.
    bar = next((y for y in range(start, size[1])
                if max(abs(int(u) - int(v)) for u, v in zip(px[column, y], tab)) <= 2), None)
    readings = {"below": ramp(start, 1), "above": ramp(above, -1)}
    # The air between the composer's border and the tab bar's is a layout number
    # the artboards fix at 12dp, and it was 18 until the spacer was traced.
    readings["gap"] = round((bar - start) / density, 1) if bar else None
    return readings


def compare(label, shot_name, edge, want, got, tolerance):
    peak, built = max(want), max(got)
    if peak < MIN_ARTBOARD_PEAK:
        print(f"SKIP {label} {shot_name} {edge}: artboard peaks at {peak}, nothing measurable to gate")
        return 0
    worst = max(abs(a - b) for a, b in zip(want, got))
    overs = [f"{k}dp want {a} +/-{tolerance} got {b}"
             for k, (a, b) in enumerate(zip(want, got), 1) if abs(a - b) > tolerance]
    if overs:
        print(f"FAIL {label} {shot_name} {edge}: " + ", ".join(overs))
        return 1
    print(f"ok   {label} {shot_name} {edge}: peak {built} vs {peak}, worst {worst} levels")
    return 0


def stroke_of(px, size, fill, density):
    """The colour drawn one dp under the capsule's fill — its border, or nothing."""
    w, _ = size
    column = w // 2
    _, bottom = capsule(px, size, fill, column)
    return px[column, bottom + round(density)]


def main():
    bad = 0
    for art_name, shot_name, fill, canvas in WINDOWS_CASES:
        art = ROOT / "design" / "pixso-final" / art_name
        shot = SHOTS / shot_name
        if not art.exists():
            print(f"MISSING {art_name}")
            bad += 1
            continue
        if not shot.exists():
            print(f"MISSING {shot_name} — run scripts/ui-shot.ps1 first")
            bad += 1
            continue
        want = windows_readings(load(art)[0], fill, canvas)
        got = windows_readings(load(shot)[0], fill, canvas)
        for edge in ("below", "above", "right"):
            bad += compare("win", shot_name, edge, want[edge], got[edge], WINDOWS_TOLERANCE)

    for art_name, shot_name, fill, canvas, want_border, want_tab in PHONE_CASES:
        art = ROOT / "design" / "pixso-final" / art_name
        shot = SHOTS / shot_name
        if not art.exists():
            print(f"MISSING {art_name}")
            bad += 1
            continue
        if not shot.exists():
            print(f"MISSING {shot_name} — run scripts/ui-shot-android.sh first")
            bad += 1
            continue
        a_px, a_size = load(art)
        b_px, b_size = load(shot)
        want = phone_readings(a_px, a_size, fill, canvas, 1.0, want_tab)
        got = phone_readings(b_px, b_size, fill, canvas, PHONE_DENSITY, want_tab)
        for edge in ("below", "above"):
            bad += compare("android", shot_name, edge, want[edge], got[edge], ANDROID_TOLERANCE)
        wg, bg = want["gap"], got["gap"]
        if wg is None or bg is None:
            print(f"FAIL android {shot_name} gap: no tab-bar hairline below the composer")
            bad += 1
        elif abs(wg - bg) > 2:
            print(f"FAIL android {shot_name} gap: composer->tabbar is {bg}dp, artboard draws {wg}dp")
            bad += 1
        else:
            print(f"ok   android {shot_name} gap: composer->tabbar {bg}dp vs {wg}dp")

        built = stroke_of(b_px, b_size, fill, PHONE_DENSITY)
        if built != want_border:
            print(f"FAIL android {shot_name} stroke: composer border is {built}, artboard draws {want_border}")
            bad += 1
        else:
            print(f"ok   android {shot_name} stroke: composer border {built}")

        # The tab bar's own halo lands in the navigation-bar inset, which the OS
        # composites over, so only its stroke is assertable here.
        _, bottom = capsule(b_px, b_size, fill, b_size[0] // 2)
        runs = [y for y in range(bottom, b_size[1]) if b_px[b_size[0] // 2, y] == want_tab]
        if len(runs) < round(PHONE_DENSITY):
            print(f"FAIL android {shot_name} tabbar stroke: no {want_tab} hairline under the composer")
            bad += 1
        else:
            print(f"ok   android {shot_name} tabbar stroke: {want_tab} at y {min(runs)}..{max(runs)}")
    return 1 if bad else 0


if __name__ == "__main__":
    sys.exit(main())

#!/usr/bin/env python3
"""Side-by-side a real screenshot with its Pixso artboard and print the band diff.

    python scripts/ui-diff.py 02-windows-起始页-深色 artifacts/shots/now-01-dark.png

Writes artifacts/shots/diff-<stem>.png (design left, build right) and lists the
ink bands of the sidebar and the content column for both, so a layout drift is a
number rather than an impression.
"""

import pathlib
import sys

from PIL import Image

sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))
from ui_measure import bands, load  # noqa: E402


def main():
    if len(sys.argv) < 3:
        print(__doc__)
        return 2
    # Accept either an artboard number or a file name, so a Chinese file name
    # never has to survive the shell's encoding round trip.
    arg = pathlib.Path(sys.argv[1])
    design = arg if arg.exists() else sorted(
        pathlib.Path("design/pixso-final").glob(f"{arg.stem}-*.png"))[0]
    shot = pathlib.Path(sys.argv[2])

    dpx, (dw, dh) = load(design)
    spx, (sw, sh) = load(shot)

    out = pathlib.Path("artifacts/shots") / f"diff-{shot.stem}.png"
    canvas = Image.new("RGB", (dw + sw + 16, max(dh, sh)), (255, 0, 255))
    canvas.paste(Image.open(design).convert("RGB"), (0, 0))
    canvas.paste(Image.open(shot).convert("RGB"), (dw + 16, 0))
    canvas.save(out)

    for label, px, size in ((f"design {design.name}", dpx, (dw, dh)),
                            (f"build  {shot.name}", spx, (sw, sh))):
        canvas_bg = px[700, 300]
        sidebar_bg = px[100, 300]
        print(f"--- {label}")
        print("   sidebar", bands(px, 12, 228, 44, size[1], sidebar_bg, 10))
        print("   content", bands(px, 336, 1344, 44, size[1], canvas_bg, 14))
    print(f"side by side -> {out}")
    return 0


if __name__ == "__main__":
    sys.exit(main())

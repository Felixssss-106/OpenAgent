"""Compare landmark colours between a Pixso artboard and the shipped build's screenshot."""
import pathlib
import sys

import numpy as np
from PIL import Image
from collections import Counter


def art(number):
    return str(sorted(pathlib.Path("design/pixso-final").glob(f"{number}-*.png"))[0])


def modal(path, x0, y0, x1, y1):
    region = np.array(Image.open(path).convert("RGB")).astype(int)[y0:y1, x0:x1]
    counts = Counter(map(tuple, region.reshape(-1, 3)))
    return tuple(int(v) for v in counts.most_common(1)[0][0])


def bluest(path):
    a = np.array(Image.open(path).convert("RGB")).astype(int)
    score = a[..., 2] - (a[..., 0] + a[..., 1]) / 2
    y, x = np.unravel_index(np.argmax(score), score.shape)
    return tuple(int(v) for v in a[y, x]), (int(x), int(y))


def main():
    cases = [
        ("light", "01", "artifacts/shots/acc-agent-light.png"),
        ("dark", "02", "artifacts/shots/acc-agent-dark.png"),
    ]
    for theme, number, build in cases:
        design = art(number)
        print(f"--- {theme} ---")
        for label, box in (
            ("capsule band", (340, 541, 760, 556)),
            ("sidebar label", (44, 240, 120, 250)),
            ("nav row bg", (30, 236, 210, 248)),
        ):
            d = modal(design, *box)
            b = modal(build, *box)
            delta = round(float(np.sqrt(sum((p - q) ** 2 for p, q in zip(d, b)))), 1)
            mark = "   <-- differs" if delta > 12 else ""
            print(f"  {label:14s} design {str(d):20s} build {str(b):20s} dE {delta}{mark}")
        (d, dpos) = bluest(design)
        (b, bpos) = bluest(build)
        delta = round(float(np.sqrt(sum((p - q) ** 2 for p, q in zip(d, b)))), 1)
        print(f"  {'accent':14s} design {str(d):20s} build {str(b):20s} dE {delta} at {dpos}/{bpos}")


if __name__ == "__main__":
    sys.exit(main())

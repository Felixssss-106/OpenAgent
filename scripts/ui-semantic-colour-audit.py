"""Compare the sets of saturated (semantic) colours a design frame and a build
screenshot contain, without needing to know where each element sits.

    python scripts/ui-semantic-colour-audit.py 06 artifacts/shots/acc-approve-dark.png
"""
import pathlib
import sys
from collections import Counter

import numpy as np
from PIL import Image

STEP = 12


def palette(path, min_sat=45, top=6):
    a = np.array(Image.open(path).convert("RGB")).astype(int)
    sat = a.max(axis=2) - a.min(axis=2)
    px = a[sat > min_sat]
    if len(px) < 40:
        return []
    quantized = (px // STEP * STEP)
    counts = Counter(map(tuple, quantized))
    return [(colour, n) for colour, n in counts.most_common(top)]


def nearest(colour, others, limit=STEP * 2):
    best, best_d = None, 1e9
    for other, n in others:
        d = float(np.sqrt(sum((p - q) ** 2 for p, q in zip(colour, other))))
        if d < best_d:
            best, best_d = (other, n), d
    return best, best_d


def main():
    if len(sys.argv) < 3:
        print(__doc__)
        return 2
    design_path = pathlib.Path(sys.argv[1])
    if not design_path.exists():
        found = sorted(pathlib.Path("design/pixso-final").glob(f"{design_path.stem}-*.png"))
        if not found:
            print(f"no artboard matching {sys.argv[1]}")
            return 1
        design_path = found[0]
    build_path = pathlib.Path(sys.argv[2])

    d, b = palette(design_path), palette(build_path)
    print(f"design {design_path.name}  vs  build {build_path.name}")
    unmatched = 0
    for colour, n in d:
        match, dist = nearest(colour, b)
        if match is None or dist > STEP * 2:
            unmatched += 1
            print(f"  design-only {colour}  ({n}px)  nearest build {match} d={dist:.0f}")
        else:
            print(f"  ok          {colour} ({n}px)  ~ build {match[0]} ({match[1]}px) d={dist:.0f}")
    for colour, n in b:
        if nearest(colour, d)[1] > STEP * 2:
            unmatched += 1
            print(f"  build-only  {colour}  ({n}px)")
    print(f"unmatched semantic colours: {unmatched}")
    return 0 if unmatched == 0 else 1


if __name__ == "__main__":
    sys.exit(main())

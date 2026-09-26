import importlib.util
import numpy as np
from PIL import Image

spec = importlib.util.spec_from_file_location("b", "scripts/ui-band-sweep.py")
m = importlib.util.module_from_spec(spec)
spec.loader.exec_module(m)

MIN_RUN = 200


def rings(path):
    """Long straight runs of the focus-adorner colour inside the content column.

    Light theme draws it near-black, dark theme near-white; the page background is the
    opposite, so thresholding on the median of an empty strip keeps the two apart.
    """
    a = np.array(Image.open(path).convert("RGB")).astype(int)
    bg = int(np.median(a[100, 700]))
    want = np.array([26, 26, 26]) if bg > 200 else np.array([232, 232, 232])
    mask = (np.abs(a - want).max(axis=2) <= 40)
    mask[:, :330] = False
    found = []
    for y in range(a.shape[0]):
        run = np.where(mask[y])[0]
        if len(run) >= MIN_RUN:
            found.append(("h", y, int(run.min()), int(run.max())))
    return found


bad = 0
for page in m.PAGES:
    for theme in ("light", "dark"):
        got = rings(f"artifacts/shots/cur-{page}-{theme}.png")
        if got:
            bad += 1
            print(f"  RING {page}/{theme}: {got[:2]}")
print(f"captures with an adorner: {bad}")
raise SystemExit(1 if bad else 0)

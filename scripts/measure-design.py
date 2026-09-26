#!/usr/bin/env python3
"""Measure element geometry out of a Pixso export PNG.

Finds the bounding box of the pixels that differ from the local background, so
the design's paddings, heights and corner radii can be read off the mockup
instead of estimated by eye.

    python scripts/measure-design.py design/pixso-final/01-windows-起始页-浅色.png
    python scripts/measure-design.py <png> --region x0,y0,x1,y1 --probe "name:x,y"
"""

import argparse
import sys

from PIL import Image


def load(path):
    img = Image.open(path).convert("RGB")
    return img, img.load(), img.size


def color_at(px, x, y):
    return px[int(x), int(y)]


def near(a, b, tol):
    return all(abs(int(u) - int(v)) <= tol for u, v in zip(a, b))


def bbox_of(px, size, region, ref, tol):
    """Bounding box of pixels inside region whose colour differs from ref."""
    x0, y0, x1, y1 = region
    x1 = min(x1, size[0])
    y1 = min(y1, size[1])
    minx = miny = 10**9
    maxx = maxy = -1
    for y in range(y0, y1):
        for x in range(x0, x1):
            if near(px[x, y], ref, tol):
                continue
            minx, miny = min(minx, x), min(miny, y)
            maxx, maxy = max(maxx, x), max(maxy, y)
    if maxx < 0:
        return None
    return (minx, miny, maxx, maxy)


def scan_row(px, size, y, ref, tol):
    """Runs of non-background pixels along one row."""
    runs, start = [], None
    for x in range(size[0]):
        hit = not near(px[x, y], ref, tol)
        if hit and start is None:
            start = x
        elif not hit and start is not None:
            runs.append((start, x - 1))
            start = None
    if start is not None:
        runs.append((start, size[0] - 1))
    return [r for r in runs if r[1] - r[0] > 2]


def mean_column(px, x, y0, y1):
    acc = [0, 0, 0]
    n = max(1, y1 - y0)
    for y in range(y0, y1):
        for i in range(3):
            acc[i] += px[x, y][i]
    return tuple(v // n for v in acc)


def mean_row(px, y, x0, x1):
    acc = [0, 0, 0]
    n = max(1, x1 - x0)
    for x in range(x0, x1):
        for i in range(3):
            acc[i] += px[x, y][i]
    return tuple(v // n for v in acc)


def column_edges(px, size, y0, y1, tol):
    """Column indices where the column's mean colour changes from its neighbour."""
    out = []
    prev = None
    for x in range(size[0]):
        acc = mean_column(px, x, y0, y1)
        if prev is not None and not near(acc, prev, tol):
            out.append((x, prev, acc))
        prev = acc
    return out


def row_edges(px, size, x0, x1, tol):
    out = []
    prev = None
    for y in range(size[1]):
        acc = mean_row(px, y, x0, x1)
        if prev is not None and not near(acc, prev, tol):
            out.append((y, prev, acc))
        prev = acc
    return out


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("png")
    ap.add_argument("--region", default=None, help="x0,y0,x1,y1 restrict the search")
    ap.add_argument("--tol", type=int, default=6)
    ap.add_argument("--probe", action="append", default=[], help="name:x,y")
    args = ap.parse_args()

    img, px, size = load(args.png)
    print(f"image {size[0]}x{size[1]}")

    for p in args.probe:
        name, _, coord = p.partition(":")
        x, _, y = coord.partition(",")
        print(f"  probe {name} @ ({x},{y}) = {color_at(px, float(x), float(y))}")

    region = (0, 0, size[0], size[1])
    if args.region:
        region = tuple(int(v) for v in args.region.split(","))
    corner = color_at(px, region[0], region[1])
    print(f"region {region} corner colour {corner}")

    print("horizontal bands (full width, left half excluded to skip sidebar):")
    for y, a, b in row_edges(px, size, region[2] // 2, region[2], args.tol):
        if region[1] <= y <= region[3]:
            print(f"  y={y}: {a} -> {b}")

    print("vertical bands (content column):")
    for x, a, b in column_edges(px, size, region[1], region[3], args.tol):
        if region[0] <= x <= region[2]:
            print(f"  x={x}: {a} -> {b}")

    box = bbox_of(px, size, region, corner, args.tol)
    print(f"content bbox in region: {box}")
    if box:
        for y in (box[1], (box[1] + box[3]) // 2, box[3]):
            print(f"  row y={y} runs: {scan_row(px, size, y, corner, args.tol)[:12]}")
    return 0


if __name__ == "__main__":
    sys.exit(main())

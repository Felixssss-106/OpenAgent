#!/usr/bin/env python3
"""Print the centre of the uiautomator node whose text matches, from stdin.

    adb shell uiautomator dump /sdcard/oa-dump.xml
    adb exec-out cat /sdcard/oa-dump.xml | python scripts/android-tab.py 任务

Compose draws the tab label as a Text node, so its bounds are the tap target.
"""

import re
import sys

xml = sys.stdin.buffer.read().decode("utf-8", "replace")
wanted = sys.argv[1]

for match in re.finditer(r'text="' + re.escape(wanted) + r'"[^>]*bounds="\[(\d+),(\d+)\]\[(\d+),(\d+)\]"', xml):
    x1, y1, x2, y2 = map(int, match.groups())
    print((x1 + x2) // 2, (y1 + y2) // 2)
    break

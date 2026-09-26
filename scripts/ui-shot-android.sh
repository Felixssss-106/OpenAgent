#!/usr/bin/env bash
# Screenshot the shipped Android build on a headless emulator, in a chosen theme.
#
#   scripts/ui-shot-android.sh [route] [theme]     # route: agent|tasks|devices|settings
#   BUILD=0 scripts/ui-shot-android.sh             # skip gradle + install
#   APK=/path/to/x.apk scripts/ui-shot-android.sh  # override which artifact
#
# Defaults to the **release** APK: the objective is about the software as published, and
# the release build is the artifact users install. It is not debuggable, so the theme is
# set with the emulator's own night mode rather than by writing the app's SharedPreferences
# (which only `run-as` on a debuggable build could do).
#
# Writes two files and never one over the other:
#   artifacts/shots/raw-<route>-<theme>.png       unscaled 1080x2400, what the audit reads
#   artifacts/shots/android-<route>-<theme>.png   390x844, what a person compares by eye
# Resizing in place used to destroy the hairline the audit looks for, so both are kept.
#
# Routes are reached by tapping the tab a user would tap, so a route that screenshots
# also proves its tab works.
set -u

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
SDK="${LOCALAPPDATA:-$HOME/AppData/Local}/Android/Sdk"
SDK="${SDK//\\//}"
ADB="$SDK/platform-tools/adb.exe"
APK="${APK:-$ROOT/android/app/build/outputs/apk/release/app-release.apk}"
ROUTE="${1:-agent}"
THEME="${2:-light}"
RAW="$ROOT/artifacts/shots/raw-$ROUTE-$THEME.png"
SCALED="$ROOT/artifacts/shots/android-$ROUTE-$THEME.png"

case "$THEME" in
    light) night=no ;;
    dark)  night=yes ;;
    *) echo "theme must be light or dark"; exit 2 ;;
esac

if [ ! -f "$APK" ]; then
    echo "no APK at $APK"
    exit 1
fi

if [ "${BUILD:-1}" = "1" ]; then
    ( cd "$ROOT/android" && JAVA_HOME="D:/Develop/AndroidStudio/jbr" \
        ./gradlew assembleRelease --console=plain > "$ROOT/artifacts/gradle.log" 2>&1 )
    status=$?
    if [ $status -ne 0 ]; then
        grep -E "^e: |error:" "$ROOT/artifacts/gradle.log" | head -10
        echo "build failed ($status)"
        exit $status
    fi
    # A release-signed APK cannot install over a debug-signed one; clear it first.
    "$ADB" uninstall com.openagent.android > /dev/null 2>&1
    "$ADB" install -g "$APK" > /dev/null || { echo "install failed"; exit 1; }
fi

MSYS_NO_PATHCONV=1 "$ADB" -e shell cmd uimode night "$night" > /dev/null
MSYS_NO_PATHCONV=1 "$ADB" -e shell am force-stop com.openagent.android
sleep 1
MSYS_NO_PATHCONV=1 "$ADB" -e shell am start -n com.openagent.android/.MainActivity > /dev/null
sleep 7

if [ "$ROUTE" != "agent" ]; then
    label=$(case "$ROUTE" in tasks) echo 任务 ;; devices) echo 设备 ;; settings) echo 设置 ;; esac)
    MSYS_NO_PATHCONV=1 "$ADB" -e shell uiautomator dump /sdcard/oa-dump.xml > /dev/null
    point=$(MSYS_NO_PATHCONV=1 "$ADB" -e exec-out cat /sdcard/oa-dump.xml \
        | python "$ROOT/scripts/android-tab.py" "$label")
    if [ -z "$point" ]; then echo "no tab labelled $label"; exit 1; fi
    # shellcheck disable=SC2086
    MSYS_NO_PATHCONV=1 "$ADB" -e shell input tap $point
    sleep 2
fi

before=$(stat -c %Y "$RAW" 2>/dev/null || echo 0)
"$ADB" -e exec-out screencap -p > "$RAW" || { echo "screencap failed"; exit 1; }
after=$(stat -c %Y "$RAW" 2>/dev/null || echo 0)
if [ "$after" -le "$before" ]; then
    echo "FAILED: $RAW was not rewritten by this run"
    exit 1
fi

python - "$RAW" "$SCALED" <<'PY' || exit 1
import sys
import numpy as np
from PIL import Image
raw, scaled = sys.argv[1], sys.argv[2]
im = Image.open(raw).convert("RGB")
a = np.array(im).astype(float)
spread = a.std()
if spread < 12:
    # A uniform frame is the emulator drawing nothing yet (or a black screen after a
    # display power event), not an empty page: an empty page still has text.
    print(f"FAILED: {raw} is near-uniform (stddev {spread:.1f}); the frame is not a render")
    sys.exit(1)
Image.open(raw).convert("RGB").resize((390, 844)).save(scaled)
print(f"{raw} {im.size} stddev {spread:.1f} -> {scaled} 390x844")
PY

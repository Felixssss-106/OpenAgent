#!/usr/bin/env bash
# Build, install and screenshot the Android app on the headless verification
# emulator, resized to the artboard's 390x844 dp frame.
#
#   scripts/ui-shot-android.sh [route] [out.png]     # route: agent|tasks|devices|settings
#   BUILD=0 scripts/ui-shot-android.sh               # skip gradle + install
#
# Routes are reached by tapping the tab a user would tap, so a route that
# screenshots also proves its tab works.
set -u

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
SDK="${LOCALAPPDATA:-$HOME/AppData/Local}/Android/Sdk"
SDK="${SDK//\\//}"
ADB="$SDK/platform-tools/adb.exe"
APK="$ROOT/android/app/build/outputs/apk/debug/app-debug.apk"
ROUTE="${1:-agent}"
OUT="${2:-$ROOT/artifacts/shots/android-$ROUTE.png}"

if [ "${BUILD:-1}" = "1" ]; then
    ( cd "$ROOT/android" && JAVA_HOME="D:/Develop/AndroidStudio/jbr" \
        ./gradlew assembleDebug --console=plain > "$ROOT/artifacts/gradle.log" 2>&1 )
    status=$?
    if [ $status -ne 0 ]; then
        grep -E "^e: |error:" "$ROOT/artifacts/gradle.log" | head -10
        echo "build failed ($status)"
        exit $status
    fi
    "$ADB" install -r -g "$APK" > /dev/null || { echo "install failed"; exit 1; }
fi

MSYS_NO_PATHCONV=1 "$ADB" -e shell am force-stop com.openagent.android
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

"$ADB" -e exec-out screencap -p > "$OUT" || { echo "screencap failed"; exit 1; }
python - "$OUT" "$ROUTE" <<'PY'
import sys
from PIL import Image
path = sys.argv[1]
im = Image.open(path).convert("RGB")
im.resize((390, 844)).save(path)
print(f"{path} {im.size} -> 390x844")
PY

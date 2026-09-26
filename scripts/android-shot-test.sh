#!/usr/bin/env bash
# Hold a Compose test's frame on the emulator and screencap it, so a state the app
# cannot reach on an emulator (a conversation, a device card, a task row) can still be
# measured against its artboard.
#
#   scripts/android-shot-test.sh [out.png] [class#method]   # BUILD=0 skips gradle
#   THEME=dark scripts/android-shot-test.sh ...             # night mode for the frame
#
# Night mode is set on the emulator rather than in the app: the tests render through
# OpenAgentTheme, which follows isSystemInDarkTheme().
#
# Two files are written. The unscaled one ("raw-" + the same stem) is what
# scripts/ui-android-audit.py reads: a 1px hairline does not survive a resize, and
# resizing in place used to let the audit pass on a smeared border.
set -u

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
SDK="${LOCALAPPDATA:-$HOME/AppData/Local}/Android/Sdk"
SDK="${SDK//\\//}"
ADB="$SDK/platform-tools/adb.exe"
OUT="${1:-$ROOT/artifacts/shots/android-chat-test.png}"
TEST="${2:-com.openagent.android.AgentScreenTest#conversationStateDrawsEveryArtboardPart}"
RAW="$(dirname "$OUT")/raw-$(basename "$OUT")"
THEME="${THEME:-light}"
NIGHT=no
[ "$THEME" = "dark" ] && NIGHT=yes

if [ "${BUILD:-1}" = "1" ]; then
    ( cd "$ROOT/android" && JAVA_HOME="D:/Develop/AndroidStudio/jbr" \
        ./gradlew assembleDebug assembleDebugAndroidTest --console=plain \
        > "$ROOT/artifacts/gradle.log" 2>&1 )
    status=$?
    if [ $status -ne 0 ]; then
        grep -E "^e: |error:" "$ROOT/artifacts/gradle.log" | head -10
        echo "build failed ($status)"
        exit $status
    fi
fi

# A debug build cannot install over the release-signed one (different signature), and the
# Android verifier installs release first, so clear it rather than fail silently.
"$ADB" install -r -g "$ROOT/android/app/build/outputs/apk/debug/app-debug.apk" > /dev/null 2>&1 || {
    "$ADB" uninstall com.openagent.android > /dev/null 2>&1
    "$ADB" install -g "$ROOT/android/app/build/outputs/apk/debug/app-debug.apk" > /dev/null || {
        echo "FAILED: could not install the debug APK"; exit 1; }
}
"$ADB" install -r -g \
    "$ROOT/android/app/build/outputs/apk/androidTest/debug/app-debug-androidTest.apk" > /dev/null

MSYS_NO_PATHCONV=1 "$ADB" -e shell cmd uimode night "$NIGHT" > /dev/null
sleep 1

MSYS_NO_PATHCONV=1 "$ADB" -e shell am instrument -w \
    -e class "$TEST" \
    -e hold 12 \
    com.openagent.android.test/androidx.test.runner.AndroidJUnitRunner \
    > "$ROOT/artifacts/instrument.log" 2>&1 &
instrument=$!

sleep 8
before=$(stat -c %Y "$RAW" 2>/dev/null || echo 0)
"$ADB" -e exec-out screencap -p > "$RAW" || { echo "screencap failed"; exit 1; }
wait $instrument
status=$?

python - "$RAW" "$OUT" <<'PY'
import sys
import numpy as np
from PIL import Image
raw, scaled = sys.argv[1], sys.argv[2]
im = Image.open(raw).convert("RGB")
spread = np.array(im).astype(float).std()
if spread < 12:
    print(f"FAILED: {raw} is near-uniform (stddev {spread:.1f}); nothing had rendered")
    sys.exit(1)
im.resize((390, 844)).save(scaled)
print(f"{raw} {im.size} stddev {spread:.1f} -> {scaled} 390x844")
PY
python_status=$?
tail -3 "$ROOT/artifacts/instrument.log"

# `adb shell am instrument` exits 0 whether the test passed or not, so the log is the
# only verdict. Without this the script happily resized and shipped a frame of a state
# whose assertions had just failed.
if grep -qE "FAILURES!!!|Failures: [1-9]|Errors: [1-9]|INSTRUMENTATION_FAILED|^There w[as] [1-9]" \
        "$ROOT/artifacts/instrument.log"; then
    echo "FAILED: the instrumented test did not pass - see artifacts/instrument.log"
    exit 1
fi
grep -qE "^OK \(" "$ROOT/artifacts/instrument.log" || {
    echo "FAILED: no 'OK (' result line in artifacts/instrument.log"
    exit 1
}

if [ "$python_status" -ne 0 ]; then
    echo "FAILED: the captured frame is not usable"
    exit 1
fi

after=$(stat -c %Y "$RAW" 2>/dev/null || echo 0)
if [ "$after" -le "$before" ]; then
    echo "FAILED: $RAW was not rewritten by this run"
    exit 1
fi

exit $status

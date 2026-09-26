#!/usr/bin/env bash
# Hold the 对话态 test frame on the emulator and screencap it, so artboards 09/10
# can be measured without a phone on the same Wi-Fi.
#
#   scripts/android-shot-test.sh [out.png] [class#method]   # BUILD=0 skips gradle
set -u

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
SDK="${LOCALAPPDATA:-$HOME/AppData/Local}/Android/Sdk"
SDK="${SDK//\\//}"
ADB="$SDK/platform-tools/adb.exe"
OUT="${1:-$ROOT/artifacts/shots/android-chat-test.png}"
TEST="${2:-com.openagent.android.AgentScreenTest#conversationStateDrawsEveryArtboardPart}"

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

"$ADB" install -r -g "$ROOT/android/app/build/outputs/apk/debug/app-debug.apk" > /dev/null
"$ADB" install -r -g \
    "$ROOT/android/app/build/outputs/apk/androidTest/debug/app-debug-androidTest.apk" > /dev/null

MSYS_NO_PATHCONV=1 "$ADB" -e shell am instrument -w \
    -e class "$TEST" \
    -e hold 12 \
    com.openagent.android.test/androidx.test.runner.AndroidJUnitRunner \
    > "$ROOT/artifacts/instrument.log" 2>&1 &
instrument=$!

sleep 8
before=$(stat -c %Y "$OUT" 2>/dev/null || echo 0)
"$ADB" -e exec-out screencap -p > "$OUT" || { echo "screencap failed"; exit 1; }
wait $instrument
status=$?

python - "$OUT" <<'PY'
import sys
from PIL import Image
path = sys.argv[1]
im = Image.open(path).convert("RGB")
im.resize((390, 844)).save(path)
print(f"{path} -> 390x844")
PY
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

after=$(stat -c %Y "$OUT" 2>/dev/null || echo 0)
if [ "$after" -le "$before" ]; then
    echo "FAILED: $OUT was not rewritten by this run"
    exit 1
fi

exit $status

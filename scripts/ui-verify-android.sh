#!/usr/bin/env bash
# Regenerate the Android parity evidence from the released APK and audit it.
#
#   scripts/ui-verify-android.sh              # gradle + install + 4 routes x 2 themes + audit
#   BUILD=0 scripts/ui-verify-android.sh      # re-shoot only (device already provisioned)
#
# Needs a running emulator; the AVD is throwaway and the script does not create it.
set -u

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
ROUTES="${ROUTES:-agent tasks devices settings}"
THEMES="${THEMES:-light dark}"

cd "$ROOT" || exit 1

for theme in $THEMES; do
    for route in $ROUTES; do
        bash scripts/ui-shot-android.sh "$route" "$theme" || exit 1
    done
done

echo "--- Android parity (released APK) ---"
python scripts/ui-android-audit.py
audit=$?

if [ $audit -ne 0 ]; then
    echo "FAILED: the phone build drifted from the artboards"
    exit 1
fi
echo "OK: Android captures regenerated from the release APK and audited"

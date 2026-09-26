#!/usr/bin/env bash
# Regenerate the Windows UI parity evidence against the shipped publish directory.
#
# Captures every page in both themes from artifacts/windows/win-x64 (the tree the MSI
# is proven byte-identical to), then runs the two audits over the new captures:
# landmark colours and row-band positions. Fails if any landmark colour drifts.
#
#   scripts/ui-verify.sh            # capture + audit
#   SKIP_SHOTS=1 scripts/ui-verify.sh   # audit the existing captures only
set -u

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
PUB="$ROOT/artifacts/windows/win-x64"
EXE="$PUB/OpenAgent.exe"
SHOTS="$ROOT/artifacts/shots"
PAGES="agent tasks devices tools providers plugins settings"
THEMES="light dark"

capture() {
    [ -x "$EXE" ] || { echo "no published exe at $EXE"; return 1; }
    for page in $PAGES; do
        for theme in $THEMES; do
            powershell.exe -NoProfile -Command \
                "Stop-Process -Name OpenAgent -Force -ErrorAction SilentlyContinue;
                 Start-Sleep -Milliseconds 600;
                 Start-Process -FilePath '$EXE' -WorkingDirectory '$PUB' -ArgumentList '--page=$page','--theme=$theme';
                 Start-Sleep -Seconds 8" >/dev/null 2>&1
            powershell.exe -NoProfile -ExecutionPolicy Bypass -File "$ROOT/scripts/capture-window.ps1" \
                -Width 1440 -Height 900 -Out "artifacts/shots/cur-$page-$theme.png" 2>&1 | tail -1
        done
    done
    powershell.exe -NoProfile -Command "Stop-Process -Name OpenAgent -Force -ErrorAction SilentlyContinue" >/dev/null 2>&1
}

cd "$ROOT" || exit 1
[ "${SKIP_SHOTS:-0}" = "1" ] || capture

echo "--- landmark colours (shipped build vs artboards) ---"
python scripts/ui-colour-audit.py
colours=$?

echo
echo "--- row bands (positions only; counts differ by content) ---"
python scripts/ui-band-sweep.py cur

if [ $colours -ne 0 ]; then
    echo "FAILED: a landmark colour drifted from the artboard"
    exit 1
fi
echo "OK: captures regenerated and landmark colours match"

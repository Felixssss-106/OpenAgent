#!/usr/bin/env bash
# Regenerate the Windows UI parity evidence against the shipped publish directory.
#
# Captures every page in both themes from artifacts/windows/win-x64 (the tree the MSI
# is proven byte-identical to), then runs four audits over the new captures: landmark
# colours, row-band positions (report only), the sidebar and page-header geometry gate,
# and the two interactive states (artboards 03-06), which are driven, captured and
# colour-checked here rather than eyeballed. Fails if any page did not produce a fresh
# screenshot, if a landmark colour drifts, or if chrome the artboards draw is missing.
#
#   scripts/ui-verify.sh                    # capture + audit + drive the states
#   SKIP_SHOTS=1 scripts/ui-verify.sh       # audit the existing captures only
#   SKIP_STATES=1 scripts/ui-verify.sh      # skip the interactive states (needs foreground)
set -u

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
PUB="$ROOT/artifacts/windows/win-x64"
EXE="$PUB/OpenAgent.exe"
PAGES="agent tasks devices tools providers plugins settings"
THEMES="light dark"

# PowerShell is a Windows program: a /d/... MSYS path is not something it can start.
WIN_ROOT="$(cygpath -w "$ROOT")"
WIN_PUB="$(cygpath -w "$PUB")"
WIN_EXE="$(cygpath -w "$EXE")"

capture() {
    local started page theme out before after
    started=$(date +%s)
    for page in $PAGES; do
        for theme in $THEMES; do
            out="artifacts/shots/cur-$page-$theme.png"
            before=$(stat -c %Y "$out" 2>/dev/null || echo 0)
            powershell.exe -NoProfile -Command \
                "Stop-Process -Name OpenAgent -Force -ErrorAction SilentlyContinue;
                 Start-Sleep -Milliseconds 600;
                 Start-Process -FilePath '$WIN_EXE' -WorkingDirectory '$WIN_PUB' -ArgumentList '--page=$page','--theme=$theme';
                 Start-Sleep -Seconds 8" >/dev/null 2>&1
            powershell.exe -NoProfile -ExecutionPolicy Bypass -File "$WIN_ROOT\scripts\capture-window.ps1" \
                -Width 1440 -Height 900 -Out "$out" 2>&1 | tail -1
            after=$(stat -c %Y "$out" 2>/dev/null || echo 0)
            if [ "$after" -le "$started" ] || [ "$after" -le "$before" ]; then
                echo "FAILED: $out was not rewritten by this run"
                return 1
            fi
        done
    done
    powershell.exe -NoProfile -Command "Stop-Process -Name OpenAgent -Force -ErrorAction SilentlyContinue" >/dev/null 2>&1
    return 0
}

cd "$ROOT" || exit 1
if [ "${SKIP_SHOTS:-0}" != "1" ]; then
    capture || exit 1
fi

echo "--- landmark colours (shipped build vs artboards) ---"
python scripts/ui-colour-audit.py
colours=$?

echo
echo "--- row bands (positions only; counts differ by content) ---"
python scripts/ui-band-sweep.py cur

echo
echo "--- sidebar chrome (asserted, all 14 captures) ---"
python scripts/ui-band-sweep.py --gate
chrome=$?

states=0
if [ "${SKIP_STATES:-0}" != "1" ]; then
    echo
    echo "--- interactive states (03-06: driven, captured, colour-checked) ---"
    bash scripts/ui-state-verify.sh
    states=$?
fi

if [ $colours -ne 0 ]; then
    echo "FAILED: a landmark colour drifted from the artboard"
    exit 1
fi
if [ $chrome -ne 0 ]; then
    echo "FAILED: sidebar chrome the artboards draw is missing or misplaced in the build"
    exit 1
fi
if [ $states -ne 0 ]; then
    echo "FAILED: an interactive state could not be driven, captured, or matched"
    exit 1
fi
if [ "${SKIP_SHOTS:-0}" = "1" ]; then
    echo "OK: landmark colours and sidebar chrome match (existing captures, no re-shoot)"
else
    echo "OK: 14 captures regenerated, landmark colours and sidebar chrome match"
fi

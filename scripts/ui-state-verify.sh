#!/usr/bin/env bash
# Capture the two interactive AgentPage states the page switch cannot reach.
#
# `--page=` gets every static page, but artboards 03-06 draw the conversation and the
# approval card, which only exist after a prompt has been sent. This drives the shipped
# publish build the same way a person does: launch, type, Enter, shoot.
#
#   scripts/ui-state-verify.sh              # both states, both themes
#   STATES="approval" scripts/ui-state-verify.sh
#
# Each drive runs against a throwaway data directory (--data=, see App.xaml.cs). Without
# it the app reads its real store, which by now holds every task and conversation this
# script has ever produced: the page opens on a scrolled, populated list instead of the
# artboard's state, and the approval capture stops being reproducible.
set -u

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
PUB="$ROOT/artifacts/windows/win-x64"
WIN_EXE="$(cygpath -w "$PUB/OpenAgent.exe")"
WIN_PUB="$(cygpath -w "$PUB")"
WIN_ROOT="$(cygpath -w "$ROOT")"
STATES="${STATES:-approval chat}"
THEMES="${THEMES:-light dark}"

# 打开记事本 routes to app.launch, which needs approval; 查看系统信息 routes to
# system.get_info, which answers on its own. Decimal code points, not literals and not
# hex: see send-prompt.ps1.
APPROVAL_POINTS="25171,24320,35760,20107,26412"
CHAT_POINTS="26597,30475,31995,32479,20449,24687"

cd "$ROOT" || exit 1

for theme in $THEMES; do
    for state in $STATES; do
        case "$state" in
            approval) points="$APPROVAL_POINTS" ;;
            chat)     points="$CHAT_POINTS" ;;
            *) echo "unknown state: $state"; exit 2 ;;
        esac
        out="artifacts/shots/state-$state-$theme.png"
        before=$(stat -c %Y "$out" 2>/dev/null || echo 0)

        sandbox="$ROOT/artifacts/state-sandbox/$state-$theme"
        rm -rf "$sandbox"
        WIN_SANDBOX="$(cygpath -w "$sandbox")"

        powershell.exe -NoProfile -Command \
            "Stop-Process -Name OpenAgent -Force -ErrorAction SilentlyContinue;
             Start-Sleep -Milliseconds 600;
             Start-Process -FilePath '$WIN_EXE' -WorkingDirectory '$WIN_PUB' -ArgumentList '--theme=$theme',"--data=$WIN_SANDBOX";
             Start-Sleep -Seconds 8" >/dev/null 2>&1

        # No pipe on either of these: `cmd | tail` reports tail's exit code, which is 0
        # even when the command failed (AGENTS.md 8.20). Send to a log, take the status,
        # then show the log.
        log="$(mktemp)"
        powershell.exe -NoProfile -ExecutionPolicy Bypass -File "$WIN_ROOT\scripts\send-prompt.ps1" \
            -CodePoints "$points" -DelaySeconds 3 > "$log" 2>&1
        if [ $? -ne 0 ]; then
            echo "FAILED: could not drive $state/$theme"
            cat "$log"
            exit 1
        fi

        powershell.exe -NoProfile -ExecutionPolicy Bypass -File "$WIN_ROOT\scripts\capture-window.ps1" \
            -Width 1440 -Height 900 -Out "$out" > "$log" 2>&1
        if [ $? -ne 0 ]; then
            echo "FAILED: could not capture $out"
            cat "$log"
            exit 1
        fi
        tail -1 "$log"
        rm -f "$log"

        after=$(stat -c %Y "$out" 2>/dev/null || echo 0)
        if [ "$after" -le "$before" ]; then
            echo "FAILED: $out was not rewritten by this run"
            exit 1
        fi

        python scripts/ui-state-check.py "$state" "$theme" || exit 1
    done
done

powershell.exe -NoProfile -Command "Stop-Process -Name OpenAgent -Force -ErrorAction SilentlyContinue" >/dev/null 2>&1
rm -rf "$ROOT/artifacts/state-sandbox"
echo "OK: states captured (each from a clean data directory)"

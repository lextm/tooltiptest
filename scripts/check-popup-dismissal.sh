#!/bin/bash
# Runs tooltiptest end-to-end and checks whether ComboBox/menu popups get
# dismissed prematurely (the LibreWPF spurious-host-window-move bug).
#
# The app self-drives: it opens the File submenu, then the toolbar ComboBox
# dropdown, then shuts itself down ~16s after launch and writes a RESULT line
# to the log. This script just needs to launch it, wait for exit, and check
# that line.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_DIR="$(dirname "$SCRIPT_DIR")"
LOG_FILE="/tmp/tooltiptest_debug.log"

rm -f "$LOG_FILE"

echo "Building and running tooltiptest..."
dotnet run --project "$PROJECT_DIR" -c Debug > /tmp/tooltiptest_stdout.log 2>&1 &
RUN_PID=$!

# Manual timeout: the app shuts itself down ~16s after launch; give it up to
# 60s total (build + startup + run) before giving up and killing it.
for i in $(seq 1 60); do
    if ! kill -0 "$RUN_PID" 2>/dev/null; then
        break
    fi
    sleep 1
done

if kill -0 "$RUN_PID" 2>/dev/null; then
    echo "App did not exit on its own within 60s; killing it."
    kill "$RUN_PID" 2>/dev/null || true
    wait "$RUN_PID" 2>/dev/null || true
fi

if [ ! -f "$LOG_FILE" ]; then
    echo "FAIL: log file $LOG_FILE was never created (app did not start?)"
    tail -n 50 /tmp/tooltiptest_stdout.log || true
    exit 1
fi

RESULT_LINE="$(grep -m1 "RESULT:" "$LOG_FILE" || true)"

if [ -z "$RESULT_LINE" ]; then
    echo "FAIL: no RESULT line found (app did not complete its self-check)"
    tail -n 50 "$LOG_FILE"
    exit 1
fi

echo "$RESULT_LINE"

if echo "$RESULT_LINE" | grep -q "RESULT: PASS"; then
    echo "PASS: no premature popup dismissal detected"
    exit 0
else
    echo "FAIL: premature popup dismissal detected"
    exit 1
fi

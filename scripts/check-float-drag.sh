#!/bin/bash
# Float tear-out repro for AvalonDock-on-LibreWPF: dragging a docked tool-pane title should tear the
# pane out into a floating window. Like the splitter drag, tear-out shows a transient top-level window
# mid-drag, so it exercises the same overlay-window-mid-captured-drag path (see
# OpenDevelop/src/Libraries/AvalonDock/docs/librewpf.md).
#
# Starts the WPF DevFlow agent, waits for the app to report the pane-title screen bounds, sends a real
# drag through the DevFlow input endpoint, then reads the app's FLOAT_RESULT: PASS/FAIL verdict
# (manager.FloatingWindows became non-empty).
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_DIR="$(dirname "$SCRIPT_DIR")"
LOG_FILE="/tmp/tooltiptest_debug.log"

rm -f "$LOG_FILE"

echo "Building and running tooltiptest (AvalonDock float mode with DevFlow)..."
TOOLTIPTEST_AVALONDOCK_FLOAT_MODE=1 TOOLTIPTEST_DEVFLOW=1 dotnet run --project "$PROJECT_DIR" -c Debug > /tmp/tooltiptest_stdout.log 2>&1 &
RUN_PID=$!
trap 'kill "$RUN_PID" 2>/dev/null || true' EXIT

# Build/startup can be slow on a clean tree. Wait for both the visual and DevFlow endpoint.
READY_LINE=""
for i in $(seq 1 90); do
    if [ -f "$LOG_FILE" ]; then
        READY_LINE="$(grep -m1 "FLOAT_MODE_READY" "$LOG_FILE" || true)"
        if [ -n "$READY_LINE" ] && curl -fsS http://127.0.0.1:9523/api/v1/agent/status >/dev/null 2>&1; then
            break
        fi
    fi
    if ! kill -0 "$RUN_PID" 2>/dev/null; then
        break
    fi
    sleep 1
done

if [ -z "$READY_LINE" ]; then
    echo "FAIL: float mode did not become ready"
    tail -n 80 /tmp/tooltiptest_stdout.log || true
    exit 1
fi

ELEMENTS="$(curl -fsS 'http://127.0.0.1:9523/api/v1/ui/elements?automationId=FloatDragTitle&maxResults=40&maxDepth=96')"
TITLE_ID="$(printf '%s' "$ELEMENTS" | jq -r '.[] | select(.automationId == "FloatDragTitle") | .id' | head -n 1)"
if [ -z "$TITLE_ID" ] || [ "$TITLE_ID" = "null" ]; then
    echo "FAIL: DevFlow could not resolve FloatDragTitle"
    printf '%s\n' "$ELEMENTS"
    exit 1
fi

# Pull the title down-and-right well past the pane so AvalonDock undocks it into a floating window.
DRAG_RESPONSE="$(curl -fsS -X POST http://127.0.0.1:9523/api/v1/ui/actions/drag \
    -H 'Content-Type: application/json' \
    -d "{\"fromId\":\"$TITLE_ID\",\"dx\":160,\"dy\":160,\"steps\":32}")"
echo "DevFlow drag: $DRAG_RESPONSE"

RESULT_LINE=""
for i in $(seq 1 40); do
    RESULT_LINE="$(grep -m1 "FLOAT_RESULT:" "$LOG_FILE" || true)"
    [ -n "$RESULT_LINE" ] && break
    sleep 0.5
done

echo "=== float log ==="
grep -E "FLOAT_MODE_READY|FLOAT Preview|FLOAT_RESULT:" "$LOG_FILE" 2>/dev/null || true

echo "$RESULT_LINE"
echo "$RESULT_LINE" | grep -q "FLOAT_RESULT: PASS" && exit 0 || exit 1

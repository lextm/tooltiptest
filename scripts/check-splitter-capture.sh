#!/bin/bash
# Splitter / mouse-capture repro for the AvalonDock-on-LibreWPF splitter-drag bug.
#
# Starts the WPF DevFlow agent, waits for the app to report the splitters screen bounds, then sends
# a real drag through the DevFlow input endpoint. The app validates Thumb event semantics and the
# resulting pane width. A static capture-only probe is logged separately and is not the test result.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_DIR="$(dirname "$SCRIPT_DIR")"
LOG_FILE="/tmp/tooltiptest_debug.log"

rm -f "$LOG_FILE"

echo "Building and running tooltiptest (splitter mode with DevFlow)..."
# Use the in-window adorner ghost, which is exactly what the real AvalonDock LayoutGridControl uses
# (LayoutGridResizerGhostAdorner via the adorner layer). The separate-overlay-window modes remain as
# opt-in stress tests, but they do NOT reflect the real splitter and break mouse-up on macOS/GLFW.
TOOLTIPTEST_SPLITTER_MODE=1 TOOLTIPTEST_SPLITTER_ADORNER=1 TOOLTIPTEST_DEVFLOW=1 dotnet run --project "$PROJECT_DIR" -c Debug > /tmp/tooltiptest_stdout.log 2>&1 &
RUN_PID=$!
trap 'kill "$RUN_PID" 2>/dev/null || true' EXIT

# Build/startup can be slow on a clean tree. Wait for both the visual and DevFlow endpoint.
READY_LINE=""
for i in $(seq 1 90); do
    if [ -f "$LOG_FILE" ]; then
        READY_LINE="$(grep -m1 "SPLIT_MODE_READY" "$LOG_FILE" || true)"
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
    echo "FAIL: splitter did not become ready"
    tail -n 80 /tmp/tooltiptest_stdout.log || true
    exit 1
fi

ELEMENTS="$(curl -fsS 'http://127.0.0.1:9523/api/v1/ui/elements?type=Thumb&maxResults=20&maxDepth=96')"
THUMB_ID="$(printf '%s' "$ELEMENTS" | jq -r '.[] | select(.automationId == "SplitterProbeThumb") | .id' | head -n 1)"
if [ -z "$THUMB_ID" ] || [ "$THUMB_ID" = "null" ]; then
    echo "FAIL: DevFlow could not resolve SplitterProbeThumb"
    printf '%s\n' "$ELEMENTS"
    exit 1
fi

DRAG_RESPONSE="$(curl -fsS -X POST http://127.0.0.1:9523/api/v1/ui/actions/drag \
    -H 'Content-Type: application/json' \
    -d "{\"fromId\":\"$THUMB_ID\",\"dx\":80,\"dy\":0,\"steps\":24}")"
echo "DevFlow drag: $DRAG_RESPONSE"

RESULT_LINE=""
for i in $(seq 1 20); do
    RESULT_LINE="$(grep -m1 "SPLIT_RESULT:" "$LOG_FILE" || true)"
    [ -n "$RESULT_LINE" ] && break
    sleep 0.25
done

echo "=== splitter log ==="
grep -E "CAPTURE_PROBE:|SPLIT_MODE_READY|SPLIT Drag|SPLIT Lost|SPLIT_RESULT:" "$LOG_FILE" 2>/dev/null || true

echo "$RESULT_LINE"
echo "$RESULT_LINE" | grep -q "SPLIT_RESULT: PASS" && exit 0 || exit 1

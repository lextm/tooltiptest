#!/bin/bash
# Re-dock repro for AvalonDock-on-LibreWPF: a floating tool window is dragged over the main
# DockingManager and dropped onto a drop target, which should re-dock the pane. This is the reverse of
# the tear-out float (check-float-drag.sh) and the last leg of the docking round-trip.
#
# NOTE: upstream AvalonDock drives the floating-window drag engine (DragService + OverlayWindow drop
# targets) from Win32 WM_MOVING/WM_EXITSIZEMOVE via an HwndSource hook. LibreWPF's portable
# PresentationSource is not an HwndSource, so this path is expected to need porting - this script
# captures the current behavior so that work can be developed and verified quickly. See
# OpenDevelop/src/Libraries/AvalonDock/docs/librewpf.md.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_DIR="$(cd "$SCRIPT_DIR/../src/tooltiptest" && pwd)"
LOG_FILE="/tmp/tooltiptest_debug.log"

rm -f "$LOG_FILE"

echo "Building and running tooltiptest (AvalonDock re-dock mode with DevFlow)..."
TOOLTIPTEST_AVALONDOCK_DOCK_MODE=1 TOOLTIPTEST_DEVFLOW=1 dotnet run --project "$PROJECT_DIR" -c Debug > /tmp/tooltiptest_stdout.log 2>&1 &
RUN_PID=$!
trap 'kill "$RUN_PID" 2>/dev/null || true' EXIT

READY_LINE=""
for i in $(seq 1 90); do
    if [ -f "$LOG_FILE" ]; then
        READY_LINE="$(grep -m1 -E "DOCK_MODE_READY|DOCK_RESULT: FAIL" "$LOG_FILE" || true)"
        # The drag is driven by cliclick from this shell (not the DevFlow HTTP endpoint), so we only
        # need the app to report the drop coordinates - no DevFlow health dependency.
        if [ -n "$READY_LINE" ]; then
            break
        fi
    fi
    if ! kill -0 "$RUN_PID" 2>/dev/null; then
        break
    fi
    sleep 1
done

if [ -z "$READY_LINE" ]; then
    echo "FAIL: dock mode did not become ready"
    tail -n 80 /tmp/tooltiptest_stdout.log || true
    exit 1
fi

# Early setup failure (e.g. no floating window created) is a real result, not a harness error.
if printf '%s' "$READY_LINE" | grep -q "DOCK_RESULT: FAIL"; then
    echo "$READY_LINE"
    exit 1
fi

# Parse the drag handle and drop-target screen points reported by the app, then drive the drag as a
# relative delta (DevFlow drag takes fromId + dx/dy).
READY="$(grep -m1 "DOCK_MODE_READY" "$LOG_FILE")"
HX="$(printf '%s' "$READY" | sed -n 's/.*handleX=\([0-9.]*\).*/\1/p')"
HY="$(printf '%s' "$READY" | sed -n 's/.*handleY=\([0-9.]*\).*/\1/p')"
TX="$(printf '%s' "$READY" | sed -n 's/.*targetX=\([0-9.]*\).*/\1/p')"
TY="$(printf '%s' "$READY" | sed -n 's/.*targetY=\([0-9.]*\).*/\1/p')"
DX="$(awk -v a="$TX" -v b="$HX" 'BEGIN{printf "%d", a-b}')"
DY="$(awk -v a="$TY" -v b="$HY" 'BEGIN{printf "%d", a-b}')"
echo "drag handle=($HX,$HY) target=($TX,$TY) delta=($DX,$DY)"

# Drive the drag with cliclick from THIS shell: macOS Accessibility permission for posting synthetic
# events is granted to the terminal/agent context, not to the app process, so the app-spawned cliclick
# path can't move the cursor. The app already reported the handle and target SCREEN coordinates
# (DOCK_MODE_READY), so drag from the floating caption to the document drop indicator directly.
# Bring the app frontmost first so the events land on it (the floating window is topmost within it).
APP_PID="$(pgrep -f 'bin/Debug/net10.0-windows/tooltiptest' | head -1)"
if [ -n "$APP_PID" ]; then
    osascript -e "tell application \"System Events\" to set frontmost of (first process whose unix id is $APP_PID) to true" 2>/dev/null || true
    sleep 0.5
fi

# Interpolated press -> move -> release (integer screen points), matching a real user gesture.
MIDX=$(awk -v a="$HX" -v b="$TX" 'BEGIN{printf "%d",(a+b)/2}')
MIDY=$(awk -v a="$HY" -v b="$TY" 'BEGIN{printf "%d",(a+b)/2}')
Q1X=$(awk -v a="$HX" -v b="$TX" 'BEGIN{printf "%d",a+(b-a)*0.25}')
Q1Y=$(awk -v a="$HY" -v b="$TY" 'BEGIN{printf "%d",a+(b-a)*0.25}')
Q3X=$(awk -v a="$HX" -v b="$TX" 'BEGIN{printf "%d",a+(b-a)*0.75}')
Q3Y=$(awk -v a="$HY" -v b="$TY" 'BEGIN{printf "%d",a+(b-a)*0.75}')
HXI=$(awk -v v="$HX" 'BEGIN{printf "%d",v}'); HYI=$(awk -v v="$HY" 'BEGIN{printf "%d",v}')
TXI=$(awk -v v="$TX" 'BEGIN{printf "%d",v}'); TYI=$(awk -v v="$TY" 'BEGIN{printf "%d",v}')
echo "cliclick drag ($HXI,$HYI) -> ($TXI,$TYI)"
cliclick m:$HXI,$HYI dd:$HXI,$HYI w:100 dm:$Q1X,$Q1Y w:50 dm:$MIDX,$MIDY w:50 dm:$Q3X,$Q3Y w:50 dm:$TXI,$TYI w:80 dm:$TXI,$TYI w:60 du:$TXI,$TYI 2>&1 || true

RESULT_LINE=""
for i in $(seq 1 40); do
    RESULT_LINE="$(grep -m1 "DOCK_RESULT:" "$LOG_FILE" || true)"
    [ -n "$RESULT_LINE" ] && break
    sleep 0.5
done

echo "=== dock log ==="
grep -E "DOCK_MODE_READY|DOCK Preview|DOCK_RESULT:" "$LOG_FILE" 2>/dev/null | head -40 || true

echo "$RESULT_LINE"
echo "$RESULT_LINE" | grep -q "DOCK_RESULT: PASS" && exit 0 || exit 1

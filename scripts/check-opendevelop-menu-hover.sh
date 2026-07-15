#!/bin/bash
# Drives tooltiptest through its in-process DevFlow agent. Mouse movement is
# injected through the native platform path used by DevFlow.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_DIR="$(cd "$SCRIPT_DIR/../src/tooltiptest" && pwd)"
LOG_FILE="/tmp/tooltiptest_debug.log"
STDOUT_FILE="/tmp/tooltiptest_stdout.log"
DEVFLOW_URL="http://127.0.0.1:9523"

if ! command -v jq >/dev/null 2>&1; then
    echo "FAIL: jq is required for DevFlow response parsing"
    exit 1
fi

rm -f "$LOG_FILE" "$STDOUT_FILE"

echo "Building and running tooltiptest in hover mode..."
TOOLTIPTEST_HOVER_MODE=1 TOOLTIPTEST_DEVFLOW=1 \
    dotnet run --project "$PROJECT_DIR" -c Debug > "$STDOUT_FILE" 2>&1 &
RUN_PID=$!

cleanup() {
    if kill -0 "$RUN_PID" 2>/dev/null; then
        kill "$RUN_PID" 2>/dev/null || true
        wait "$RUN_PID" 2>/dev/null || true
    fi
}
trap cleanup EXIT

wait_for_log() {
    local pattern="$1"
    local description="$2"
    local attempts="${3:-100}"
    for _ in $(seq 1 "$attempts"); do
        if [ -f "$LOG_FILE" ] && grep -q "$pattern" "$LOG_FILE"; then
            return 0
        fi
        if ! kill -0 "$RUN_PID" 2>/dev/null; then
            break
        fi
        sleep 0.1
    done
    echo "FAIL: timed out waiting for $description"
    tail -n 80 "$LOG_FILE" 2>/dev/null || true
    exit 1
}

wait_for_agent() {
    for _ in $(seq 1 600); do
        if curl -fsS "$DEVFLOW_URL/api/v1/agent/status" >/dev/null 2>&1; then
            return 0
        fi
        if ! kill -0 "$RUN_PID" 2>/dev/null; then
            break
        fi
        sleep 0.1
    done
    echo "FAIL: DevFlow agent did not become ready"
    tail -n 80 "$STDOUT_FILE" || true
    exit 1
}

find_element_id() {
    local automation_id="$1"
    local response element_id
    for _ in $(seq 1 100); do
        response="$(curl -fsS "$DEVFLOW_URL/api/v1/ui/elements?automationId=$automation_id&maxResults=1")" || true
        element_id="$(jq -er '.[0].id' <<< "$response" 2>/dev/null)" || true
        if [ -n "$element_id" ]; then
            echo "$element_id"
            return 0
        fi
        sleep 0.1
    done
    return 1
}

move_to_element() {
    local automation_id="$1"
    local element_id response
    element_id="$(find_element_id "$automation_id")" || {
        echo "FAIL: DevFlow could not find $automation_id"
        exit 1
    }
    echo "Moving to $automation_id through DevFlow ($element_id)"
    for _ in $(seq 1 50); do
        response="$(curl -fsS -X POST "$DEVFLOW_URL/api/v1/ui/actions/move" \
            -H 'Content-Type: application/json' \
            -d "{\"elementId\":\"$element_id\"}")" || true
        if jq -e '.ok == true and .mode == "portable"' <<< "$response" >/dev/null 2>&1; then
            echo "DevFlow move response: $response"
            return 0
        fi
        sleep 0.1
    done
    echo "FAIL: DevFlow portable mouse move failed: $response"
    exit 1
}

wait_for_agent
wait_for_log "HOVER_STEP File menu opened" "File menu setup" 600
move_to_element "RecentFilesMenuItem"

wait_for_log "File > Recent Files SubmenuOpened fired" "Recent Files submenu"
move_to_element "RecentWorkspacesMenuItem"
wait_for_log "File > Recent Files > Workspaces SubmenuOpened fired" "Workspaces submenu"

for i in $(seq 1 30); do
    if ! kill -0 "$RUN_PID" 2>/dev/null; then
        break
    fi
    sleep 1
done

trap - EXIT
wait "$RUN_PID" 2>/dev/null || true

RESULT_LINE="$(grep -m1 "RESULT:" "$LOG_FILE" || true)"
if [ -z "$RESULT_LINE" ]; then
    echo "FAIL: no RESULT line found"
    tail -n 80 "$LOG_FILE" || true
    exit 1
fi

echo "$RESULT_LINE"
if echo "$RESULT_LINE" | grep -q "RESULT: PASS"; then
    echo "PASS: OpenDevelop-style hover menu stayed open"
    exit 0
fi

echo "FAIL: OpenDevelop-style hover menu did not stay open"
tail -n 80 "$LOG_FILE" || true
exit 1

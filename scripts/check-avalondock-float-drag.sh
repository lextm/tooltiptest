#!/bin/bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_DIR="$(cd "$SCRIPT_DIR/../src/tooltiptest" && pwd)"
LOG_FILE="/tmp/tooltiptest_debug.log"
rm -f "$LOG_FILE"

TOOLTIPTEST_AVALONDOCK_FLOAT_MODE=1 TOOLTIPTEST_DEVFLOW=1 \
    dotnet run --project "$PROJECT_DIR" -c Debug >/tmp/tooltiptest_float_stdout.log 2>&1 &
RUN_PID=$!
trap 'kill "$RUN_PID" 2>/dev/null || true' EXIT

for i in $(seq 1 90); do
    if grep -q "FLOAT_MODE_READY" "$LOG_FILE" 2>/dev/null && \
       curl -fsS http://127.0.0.1:9523/api/v1/agent/status >/dev/null 2>&1; then
        break
    fi
    kill -0 "$RUN_PID" 2>/dev/null || break
    sleep 1
done

ELEMENTS="$(curl -fsS 'http://127.0.0.1:9523/api/v1/ui/elements?type=AnchorablePaneTitle&maxResults=20&maxDepth=128')"
TITLE_ID="$(printf '%s' "$ELEMENTS" | jq -r '.[] | select(.automationId == "FloatDragTitle") | .id' | head -n 1)"
if [ -z "$TITLE_ID" ] || [ "$TITLE_ID" = "null" ]; then
    echo "FAIL: FloatDragTitle was not found"
    printf '%s\n' "$ELEMENTS"
    exit 1
fi

set +e
RESPONSE="$(curl -fsS -X POST http://127.0.0.1:9523/api/v1/ui/actions/drag \
    -H 'Content-Type: application/json' \
    -d "{\"fromId\":\"$TITLE_ID\",\"dx\":360,\"dy\":220,\"steps\":48}")"
CURL_EXIT=$?
set -e
echo "DevFlow drag: $RESPONSE"
if [ "$CURL_EXIT" -ne 0 ]; then
    echo "DevFlow request failed; process output follows:"
    tail -n 120 /tmp/tooltiptest_float_stdout.log || true
fi

RESULT=""
for i in $(seq 1 120); do
    RESULT="$(grep -m1 'FLOAT_RESULT:' "$LOG_FILE" 2>/dev/null || true)"
    [ -n "$RESULT" ] && break
    sleep 0.25
done

echo "=== AvalonDock float drag log ==="
grep -E 'FLOAT_MODE_READY|FLOAT_COORDINATES|FLOAT Preview|FLOAT_RESULT:' "$LOG_FILE" 2>/dev/null || true
echo "$RESULT"
echo "$RESULT" | grep -q 'FLOAT_RESULT: PASS'

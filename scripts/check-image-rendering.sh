#!/bin/bash
# Verifies that LibreWPF renders vector Image.Source=DrawingImage and
# Rectangle.Fill=DrawingBrush through the real on-screen composition path.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_DIR="$(dirname "$SCRIPT_DIR")"
LOG_FILE="/tmp/tooltiptest_debug.log"
STDOUT_FILE="/tmp/tooltiptest_image_stdout.log"
SCREENSHOT="/tmp/tooltiptest_image_screen.png"
IMAGE_CROP="/tmp/tooltiptest_image_probe.bmp"
BITMAP_CROP="/tmp/tooltiptest_bitmap_probe.bmp"
BRUSH_CROP="/tmp/tooltiptest_brush_probe.bmp"

rm -f "$LOG_FILE" "$STDOUT_FILE" "$SCREENSHOT" "$IMAGE_CROP" "$BITMAP_CROP" "$BRUSH_CROP"

echo "Building and running tooltiptest image probe..."
TOOLTIPTEST_IMAGE_MODE=1 dotnet run --project "$PROJECT_DIR" -c Debug > "$STDOUT_FILE" 2>&1 &
RUN_PID=$!

cleanup() {
    kill "$RUN_PID" 2>/dev/null || true
    wait "$RUN_PID" 2>/dev/null || true
}
trap cleanup EXIT

READY_LINE=""
for _ in $(seq 1 60); do
    if [ -f "$LOG_FILE" ]; then
        READY_LINE="$(grep -m1 "IMAGE_TEST_READY" "$LOG_FILE" || true)"
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
    echo "FAIL: image probe did not report readiness"
    tail -n 80 "$STDOUT_FILE" || true
    if [ -f "$LOG_FILE" ]; then tail -n 80 "$LOG_FILE"; fi
    exit 1
fi

echo "$READY_LINE"

sleep 1
screencapture -x "$SCREENSHOT"

parse_rect() {
    local key="$1"
    echo "$READY_LINE" | sed -E "s/.* ${key}=([0-9]+),([0-9]+),([0-9]+),([0-9]+).*/\\1 \\2 \\3 \\4/"
}

read -r BITMAP_X BITMAP_Y BITMAP_W BITMAP_H <<< "$(parse_rect bitmap)"
read -r IMAGE_X IMAGE_Y IMAGE_W IMAGE_H <<< "$(parse_rect image)"
read -r BRUSH_X BRUSH_Y BRUSH_W BRUSH_H <<< "$(parse_rect brush)"

SCREEN_SCALE="${TOOLTIPTEST_SCREEN_SCALE:-2}"
scale_rect() {
    python3 - "$SCREEN_SCALE" "$@" <<'PY'
import sys
scale = float(sys.argv[1])
print(" ".join(str(int(round(float(value) * scale))) for value in sys.argv[2:]))
PY
}

read -r BITMAP_X BITMAP_Y BITMAP_W BITMAP_H <<< "$(scale_rect "$BITMAP_X" "$BITMAP_Y" "$BITMAP_W" "$BITMAP_H")"
read -r IMAGE_X IMAGE_Y IMAGE_W IMAGE_H <<< "$(scale_rect "$IMAGE_X" "$IMAGE_Y" "$IMAGE_W" "$IMAGE_H")"
read -r BRUSH_X BRUSH_Y BRUSH_W BRUSH_H <<< "$(scale_rect "$BRUSH_X" "$BRUSH_Y" "$BRUSH_W" "$BRUSH_H")"

sips -c "$BITMAP_H" "$BITMAP_W" --cropOffset "$BITMAP_Y" "$BITMAP_X" -s format bmp "$SCREENSHOT" --out "$BITMAP_CROP" >/dev/null
sips -c "$IMAGE_H" "$IMAGE_W" --cropOffset "$IMAGE_Y" "$IMAGE_X" -s format bmp "$SCREENSHOT" --out "$IMAGE_CROP" >/dev/null
sips -c "$BRUSH_H" "$BRUSH_W" --cropOffset "$BRUSH_Y" "$BRUSH_X" -s format bmp "$SCREENSHOT" --out "$BRUSH_CROP" >/dev/null

python3 - "$BITMAP_CROP" "$IMAGE_CROP" "$BRUSH_CROP" <<'PY'
import struct
import sys

def read_bmp(path):
    data = open(path, "rb").read()
    if data[:2] != b"BM":
        raise SystemExit(f"{path}: not a BMP file")
    offset = struct.unpack_from("<I", data, 10)[0]
    width = struct.unpack_from("<i", data, 18)[0]
    height = struct.unpack_from("<i", data, 22)[0]
    bpp = struct.unpack_from("<H", data, 28)[0]
    compression = struct.unpack_from("<I", data, 30)[0]
    if bpp not in (24, 32) or compression not in (0, 3):
        raise SystemExit(f"{path}: unsupported BMP bpp={bpp} compression={compression}")
    if compression == 3 and bpp != 32:
        raise SystemExit(f"{path}: unsupported bitfield BMP bpp={bpp}")
    top_down = height < 0
    height = abs(height)
    stride = ((width * bpp + 31) // 32) * 4
    pixels = []
    for y in range(height):
        src_y = y if top_down else height - 1 - y
        row = offset + src_y * stride
        for x in range(width):
            i = row + x * (bpp // 8)
            b, g, r = data[i], data[i + 1], data[i + 2]
            pixels.append((r, g, b))
    return pixels

def count(pixels, predicate):
    return sum(1 for pixel in pixels if predicate(pixel))

bitmap_pixels = read_bmp(sys.argv[1])
image_pixels = read_bmp(sys.argv[2])
brush_pixels = read_bmp(sys.argv[3])

magenta = count(bitmap_pixels, lambda p: p[0] > 180 and p[2] > 180 and p[1] < 80)
orange = count(bitmap_pixels, lambda p: p[0] > 180 and 80 < p[1] < 180 and p[2] < 80)
red = count(image_pixels, lambda p: p[0] > 180 and p[1] < 90 and p[2] < 90)
green = count(image_pixels, lambda p: p[1] > 150 and p[0] < 120 and p[2] < 120)
blue = count(brush_pixels, lambda p: p[2] > 150 and p[0] < 120 and p[1] < 120)
yellow = count(brush_pixels, lambda p: p[0] > 160 and p[1] > 160 and p[2] < 120)

print(f"pixel-counts bitmap.magenta={magenta} bitmap.orange={orange} image.red={red} image.green={green} brush.blue={blue} brush.yellow={yellow}")

failures = []
if magenta < 400:
    failures.append("Bitmap Image magenta pixels missing")
if orange < 400:
    failures.append("Bitmap Image orange pixels missing")
if red < 400:
    failures.append("DrawingImage red pixels missing")
if green < 400:
    failures.append("DrawingImage green pixels missing")
if blue < 400:
    failures.append("DrawingBrush blue pixels missing")
if yellow < 400:
    failures.append("DrawingBrush yellow pixels missing")

if failures:
    print("FAIL: " + "; ".join(failures))
    raise SystemExit(1)

print("PASS: DrawingImage and DrawingBrush rendered visible pixels")
PY

STATUS=$?
exit "$STATUS"

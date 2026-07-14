# tooltiptest

`tooltiptest` is a small LibreWPF test harness used to reproduce and verify UI
bugs that are hard to cover with ordinary unit tests. It runs a real WPF window,
optionally starts the in-process DevFlow agent, and writes deterministic probe
results to `/tmp/tooltiptest_debug.log`.

The project currently references local source projects:

- `../wpf-labs/src/DevFlow/LeXtudio.DevFlow.Agent.WPF`
- `../OpenDevelop/src/Libraries/AvalonDock/source/Components/AvalonDock`

It uses the local LibreWPF SDK/package feed configured by `NuGet.config`.

## Requirements

- macOS with the LibreWPF runtime stack available.
- `dotnet` on `PATH`.
- `curl` for DevFlow-driven scripts.
- `jq` for scripts that parse DevFlow JSON responses.
- `screencapture` and `sips` for the image rendering probe.

## Basic Run

From `/Users/lextm/uno-tools/tooltiptest`:

```bash
dotnet run -c Debug
```

Most regression scenarios are selected with environment variables. The app logs
to `/tmp/tooltiptest_debug.log`; scripts also redirect stdout to `/tmp`.

To start the embedded DevFlow agent:

```bash
TOOLTIPTEST_DEVFLOW=1 dotnet run -c Debug
```

The DevFlow endpoint is `http://127.0.0.1:9523`.

## Regression Scripts

Run scripts from the `tooltiptest` directory.

```bash
./scripts/check-popup-dismissal.sh
```

Checks the original popup/menu dismissal regression. The app self-drives menu
and ComboBox popup state, exits on its own, and reports `RESULT: PASS` or
`RESULT: FAIL`.

```bash
./scripts/check-opendevelop-menu-hover.sh
```

Starts DevFlow and drives OpenDevelop-style nested menu hover behavior through
portable mouse movement. This verifies submenu hover does not collapse the menu
chain prematurely.

```bash
./scripts/check-image-rendering.sh
```

Verifies real on-screen rendering for:

- bitmap `Image.Source`
- vector `Image.Source=DrawingImage`
- `Rectangle.Fill=DrawingBrush`

The script captures the screen, crops the reported rectangles, and counts target
pixels. `TOOLTIPTEST_SCREEN_SCALE` defaults to `2` for Retina captures.

```bash
./scripts/check-splitter-capture.sh
```

Runs the splitter/mouse-capture repro with DevFlow. It drags a `Thumb` splitter
and validates `DragStarted`, `DragDelta`, `DragCompleted`, capture loss, and the
final pane width.

Useful variants:

```bash
TOOLTIPTEST_SPLITTER_ADORNER=1 ./scripts/check-splitter-capture.sh
TOOLTIPTEST_SPLITTER_POPUP=1 ./scripts/check-splitter-capture.sh
TOOLTIPTEST_SPLITTER_NO_OVERLAY=1 ./scripts/check-splitter-capture.sh
```

`TOOLTIPTEST_SPLITTER_ADORNER=1` is the preferred AvalonDock-style path because
it keeps the resize ghost inside the main window instead of using a second
top-level overlay window.

```bash
./scripts/check-avalondock-float-drag.sh
```

Verifies AvalonDock drag-to-float. The app creates a real `DockingManager`,
DevFlow drags the anchorable title, and the app checks that the pane became a
floating window.

This also validates the LibreWPF portable screen-coordinate contract for
floating windows:

- rendering remains backed by the native framebuffer scale
- `Window.Left` / `Window.Top` stay in WPF logical units
- `PointToScreen(new Point())` returns the same logical origin

The key log line is:

```text
FLOAT_COORDINATES window=... pointToScreen=... compatibilityTargetScale=...
```

The script passes only when `FLOAT_RESULT: PASS` is logged.

```bash
./scripts/check-dock-drop.sh
```

Attempts the reverse AvalonDock operation: drag an existing floating pane back
onto the main `DockingManager` drop target. This is the docking round-trip probe.
At the time this test was added, the native AvalonDock WM-moving path still
needed portable LibreWPF work, so failures here should be treated as actionable
docking behavior rather than harness startup failure.

## Environment Variables

The app recognizes these mode switches:

| Variable | Purpose |
| --- | --- |
| `TOOLTIPTEST_DEVFLOW=1` | Start the in-process DevFlow WPF agent. |
| `TOOLTIPTEST_HOVER_MODE=1` | Use OpenDevelop-style menu hover test UI. |
| `TOOLTIPTEST_IMAGE_MODE=1` | Use image/vector rendering probe UI. |
| `TOOLTIPTEST_SPLITTER_MODE=1` | Use splitter and mouse-capture repro UI. |
| `TOOLTIPTEST_SPLITTER_ADORNER=1` | Use an in-window adorner for the splitter ghost. |
| `TOOLTIPTEST_SPLITTER_POPUP=1` | Use a WPF `Popup` for the splitter ghost. |
| `TOOLTIPTEST_SPLITTER_NO_OVERLAY=1` | Disable the splitter ghost overlay control experiment. |
| `TOOLTIPTEST_AVALONDOCK_FLOAT_MODE=1` | Use AvalonDock drag-to-float repro UI. |
| `TOOLTIPTEST_AVALONDOCK_DOCK_MODE=1` | Use AvalonDock floating-pane re-dock repro UI. |
| `TOOLTIPTEST_SCREEN_SCALE=2` | Physical screenshot scale used by the image script. |

## Logs And Artifacts

Common files:

- `/tmp/tooltiptest_debug.log`: app-side structured probe log.
- `/tmp/tooltiptest_stdout.log`: stdout/stderr for most scripts.
- `/tmp/tooltiptest_float_stdout.log`: stdout/stderr for float-drag script.
- `/tmp/tooltiptest_image_stdout.log`: stdout/stderr for image rendering script.
- `/tmp/tooltiptest_image_screen.png`: full screenshot from image rendering probe.
- `/tmp/tooltiptest_*_probe.bmp`: cropped image-rendering probe regions.

When a script fails, inspect the matching stdout file first, then
`/tmp/tooltiptest_debug.log`.

## Typical LibreWPF Fix Loop

After changing LibreWPF code, rebuild or repack the local package used by this
project, clear stale outputs when necessary, then rerun the focused probe:

```bash
rm -rf bin obj nuget-cache/librewpf.transport
./scripts/check-avalondock-float-drag.sh
./scripts/check-image-rendering.sh
./scripts/check-splitter-capture.sh
./scripts/check-popup-dismissal.sh
```

For the floating-window DPI bug, the minimum expected validation is:

- `check-avalondock-float-drag.sh` passes.
- The `FLOAT_COORDINATES` `window=` and `pointToScreen=` values match in logical
  units.
- `check-image-rendering.sh` still passes, proving Retina rendering was not
  downgraded while fixing screen coordinate mapping.

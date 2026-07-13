using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Automation;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Shapes;

namespace ToolTipTest;

public partial class MainWindow : Window
{
    Popup? rawPopup;
    bool expectingSubmenuClose;
    bool submenuPrematureClose;
    bool nestedSubmenuOpened;
    bool nestedSubmenuPrematureClose;
    bool lazySubmenuRebuilt;
    MenuItem? recentWorkspacesMenuItem;
    bool comboPrematureClose;
    bool isVerifying;
    readonly bool hoverMode = Environment.GetEnvironmentVariable("TOOLTIPTEST_HOVER_MODE") == "1";
    readonly bool imageMode = Environment.GetEnvironmentVariable("TOOLTIPTEST_IMAGE_MODE") == "1";
    readonly bool splitterMode = Environment.GetEnvironmentVariable("TOOLTIPTEST_SPLITTER_MODE") == "1";
    Image? bitmapImageProbe;
    Image? vectorImageProbe;
    Rectangle? drawingBrushProbe;

    public MainWindow()
    {
        InitializeComponent();

        if (imageMode)
        {
            ConfigureImageMode();
            return;
        }

        if (splitterMode)
        {
            ConfigureSplitterMode();
            return;
        }

        ToolTipService.SetToolTip(ServiceButton, "Service button tooltip text");
        ToolTipService.SetInitialShowDelay(ServiceButton, 0);
        ToolTipService.SetInitialShowDelay(PlainButton, 0);

        var probeToolTip = new ToolTip
        {
            Content = "Explicit template text",
            PlacementTarget = ProbeButton,
            Style = (Style)FindResource("ProbeToolTipStyle")
        };
        ToolTipService.SetToolTip(ProbeButton, probeToolTip);
        ToolTipService.SetInitialShowDelay(ProbeButton, 0);
        probeToolTip.Opened += (s, e) => DumpToolTipTree(probeToolTip, "explicit");

        PlainButton.ToolTipOpening += (s, e) => Log("PlainButton.ToolTipOpening fired");
        PlainButton.ToolTipClosing += (s, e) => Log("PlainButton.ToolTipClosing fired");
        ServiceButton.ToolTipOpening += (s, e) => Log("ServiceButton.ToolTipOpening fired");

        PlainButton.MouseEnter += (s, e) => Log("PlainButton.MouseEnter fired");
        PlainButton.MouseLeave += (s, e) => Log("PlainButton.MouseLeave fired");

        // Auto-open a raw Popup ~2.5s after load so we can observe rendering without relying on
        // click automation landing on the button.
        Loaded += (s, e) =>
        {
            Log("MainWindow.Loaded fired");
            if (hoverMode)
            {
                Activate();
                var hoverReadyTimer = new System.Windows.Threading.DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(1)
                };
                hoverReadyTimer.Tick += (ts, te) =>
                {
                    hoverReadyTimer.Stop();
                    MainMenu.Focus();
                    FileMenuItem.IsSubmenuOpen = true;
                    Log("HOVER_STEP File menu opened by test setup");
                    StartHoverModeVerificationTimer();
                };
                hoverReadyTimer.Start();
                return;
            }

            var timer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(2.5)
            };
            timer.Tick += (ts, te) =>
            {
                timer.Stop();
                Log("Auto-open timer fired; opening raw Popup");
                OnForcePopupClick(this, new RoutedEventArgs());
            };
            timer.Start();

            // Also auto-open the ServiceButton's ToolTip (the original reported bug) a bit later.
            var ttTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(4.5)
            };
            ttTimer.Tick += (ts, te) =>
            {
                ttTimer.Stop();
                Log("Auto-open timer fired; opening ServiceButton ToolTip");
                object? tooltip = ToolTipService.GetToolTip(ServiceButton);
                if (tooltip is ToolTip tt)
                {
                    tt.PlacementTarget = ServiceButton;
                    tt.IsOpen = true;
                }
                else
                {
                    var wrapper = new ToolTip { Content = tooltip, PlacementTarget = ServiceButton, IsOpen = true };
                }
            };
            ttTimer.Start();

            var probeTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(7.5)
            };
            probeTimer.Tick += (ts, te) =>
            {
                probeTimer.Stop();
                if (rawPopup != null)
                    rawPopup.IsOpen = false;
                Log("Auto-open timer fired; opening explicit ProbeButton ToolTip");
                if (ToolTipService.GetToolTip(ProbeButton) is ToolTip probe)
                {
                    probe.PlacementTarget = ProbeButton;
                    probe.IsOpen = true;
                    Log("Explicit probe IsOpen=" + probe.IsOpen);
                }
            };
            probeTimer.Start();

            var menuTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(10)
            };
            menuTimer.Tick += (ts, te) =>
            {
                menuTimer.Stop();
                Log("Auto-open timer fired; opening main File menu Popup");
                MainMenu.Focus();
                FileMenuItem.IsSubmenuOpen = true;
            };
            menuTimer.Start();

            var recentFilesTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(11)
            };
            recentFilesTimer.Tick += (ts, te) =>
            {
                recentFilesTimer.Stop();
                Log("Auto-open timer fired; opening File > Recent Files submenu");
                RecentFilesMenuItem.IsSubmenuOpen = true;
            };
            recentFilesTimer.Start();

            var recentWorkspacesTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(12)
            };
            recentWorkspacesTimer.Tick += (ts, te) =>
            {
                recentWorkspacesTimer.Stop();
                Log("Auto-open timer fired; opening File > Recent Files > Workspaces submenu");
                if (recentWorkspacesMenuItem != null)
                    recentWorkspacesMenuItem.IsSubmenuOpen = true;
                else
                    Log("File > Recent Files > Workspaces submenu is not available after lazy rebuild");
            };
            recentWorkspacesTimer.Start();

            var comboTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(13)
            };
            comboTimer.Tick += (ts, te) =>
            {
                comboTimer.Stop();
                expectingSubmenuClose = true;
                if (recentWorkspacesMenuItem != null)
                    recentWorkspacesMenuItem.IsSubmenuOpen = false;
                RecentFilesMenuItem.IsSubmenuOpen = false;
                FileMenuItem.IsSubmenuOpen = false;
                Log("Auto-open timer fired; opening toolbar ComboBox dropdown");
                ToolbarComboBox.Focus();
                ToolbarComboBox.IsDropDownOpen = true;
            };
            comboTimer.Start();

            var verifyTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(16)
            };
            verifyTimer.Tick += (ts, te) =>
            {
                verifyTimer.Stop();
                isVerifying = true;
                bool pass = nestedSubmenuOpened &&
                    lazySubmenuRebuilt &&
                    !submenuPrematureClose &&
                    !nestedSubmenuPrematureClose &&
                    !comboPrematureClose;
                Log("RESULT: " + (pass ? "PASS" : "FAIL") +
                    " nestedSubmenuOpened=" + nestedSubmenuOpened +
                    " lazySubmenuRebuilt=" + lazySubmenuRebuilt +
                    " submenuPrematureClose=" + submenuPrematureClose +
                    " nestedSubmenuPrematureClose=" + nestedSubmenuPrematureClose +
                    " comboPrematureClose=" + comboPrematureClose);
                Application.Current.Shutdown();
            };
            verifyTimer.Start();
        };
    }

    void ConfigureImageMode()
    {
        Title = "ToolTipTest Image Probe";
        Width = 360;
        Height = 260;
        Left = 80;
        Top = 80;
        WindowStartupLocation = WindowStartupLocation.Manual;
        Background = Brushes.White;

        var panel = new StackPanel
        {
            Margin = new Thickness(24),
            Orientation = Orientation.Horizontal,
            Background = Brushes.White
        };

        bitmapImageProbe = new Image
        {
            Width = 96,
            Height = 96,
            Stretch = Stretch.Fill,
            Source = CreateProbePngBitmapSource(),
            Margin = new Thickness(0, 0, 32, 0)
        };

        vectorImageProbe = new Image
        {
            Width = 96,
            Height = 96,
            Stretch = Stretch.Fill,
            Source = CreateProbeDrawingImage(),
            Margin = new Thickness(0, 0, 32, 0)
        };

        drawingBrushProbe = new Rectangle
        {
            Width = 96,
            Height = 96,
            Fill = CreateProbeDrawingBrush(),
            Stroke = Brushes.Black,
            StrokeThickness = 1
        };

        panel.Children.Add(bitmapImageProbe);
        panel.Children.Add(vectorImageProbe);
        panel.Children.Add(drawingBrushProbe);
        Content = panel;

        Loaded += (s, e) =>
        {
            Activate();
            LogImageProbeReady();
        };
    }

    static DrawingImage CreateProbeDrawingImage()
    {
        var group = new DrawingGroup();
        group.Children.Add(new GeometryDrawing(Brushes.Transparent, null, new RectangleGeometry(new Rect(0, 0, 16, 16))));
        group.Children.Add(new GeometryDrawing(Brushes.Red, null, Geometry.Parse("M 2,2 L 14,2 L 14,14 L 2,14 Z")));
        group.Children.Add(new GeometryDrawing(Brushes.Lime, null, Geometry.Parse("M 4,4 L 12,4 L 12,12 L 4,12 Z")));
        return new DrawingImage(group);
    }

    static DrawingBrush CreateProbeDrawingBrush()
    {
        var group = new DrawingGroup();
        group.Children.Add(new GeometryDrawing(Brushes.Transparent, null, new RectangleGeometry(new Rect(0, 0, 16, 16))));
        group.Children.Add(new GeometryDrawing(Brushes.Blue, null, Geometry.Parse("M 2,2 L 14,2 L 14,14 L 2,14 Z")));
        group.Children.Add(new GeometryDrawing(Brushes.Yellow, null, Geometry.Parse("M 4,4 L 12,4 L 12,12 L 4,12 Z")));
        return new DrawingBrush(group)
        {
            Stretch = Stretch.Fill,
            TileMode = TileMode.None
        };
    }

    static BitmapSource CreateProbePngBitmapSource()
    {
        const string pngBase64 = "iVBORw0KGgoAAAANSUhEUgAAABAAAAAQCAYAAAAf8/9hAAAAIUlEQVR4nGP438DwnxLMMBwNACK8eNSAkWHAwKdEehsAANHgnd9KEgaZAAAAAElFTkSuQmCC";
        using var stream = new MemoryStream(Convert.FromBase64String(pngBase64), writable: false);

        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.StreamSource = stream;
        image.EndInit();
        image.Freeze();
        return image;
    }

    // ---- Splitter / mouse-capture repro (TOOLTIPTEST_SPLITTER_MODE=1) --------------------------
    // Minimal isolation of the AvalonDock splitter-drag bug on LibreWPF/macOS (see
    // OpenDevelop doc/technotes, AvalonDock LayoutGridControl.ShowResizerOverlayWindow): a Thumb
    // splitter captures the mouse on mouse-down and, on DragStarted, shows a *separate transparent
    // top-level Window* as the resize ghost. Suspicion: showing that window drops/steals the
    // managed mouse capture (no cross-window OS capture in the SilkNet backend), so DragDelta/Up
    // never reach the Thumb, the drag fails, and the cursor gets stuck. This strips AvalonDock away
    // to test that mechanism directly.
    Window? splitterOverlay;
    Popup? splitterPopup;
    Thumb? splitterThumb;
    int splitterDragStarted, splitterDragDelta, splitterDragCompleted, splitterLostCapture;
    double splitterHorizontalChange;
    double splitterInitialWidth;
    double splitterOverlayStartX;
    TextBlock? splitterStatus;
    // TOOLTIPTEST_SPLITTER_POPUP=1: host the resize ghost in a WPF Popup instead of a top-level
    // Window. On LibreWPF a Popup still shows as a native window, but it registers via
    // WpfPortablePopupActivation so HasAnyOpenPopup suppresses the main window's spurious
    // Deactivated - the escape hatch a plain Window doesn't get. If the drag survives with this,
    // swapping AvalonDock's ShowResizerOverlayWindow to a Popup is the fix.
    readonly bool splitterUsePopup = Environment.GetEnvironmentVariable("TOOLTIPTEST_SPLITTER_POPUP") == "1";
    // TOOLTIPTEST_SPLITTER_ADORNER=1: host the resize ghost in an in-window AdornerLayer - NO second
    // native window at all. If the drag survives with this, an Adorner-based ShowResizerOverlayWindow
    // is the AvalonDock fix (a plain Window and a Popup both fail because both are native windows on
    // LibreWPF and showing one mid-drag injects a phantom MouseUp).
    readonly bool splitterUseAdorner = Environment.GetEnvironmentVariable("TOOLTIPTEST_SPLITTER_ADORNER") == "1";
    GhostAdorner? splitterAdorner;

    static string CapturedName() => Mouse.Captured?.GetType().Name ?? "null";
    static string Fmt(Point p) => $"{p.X:0},{p.Y:0}";

    void ConfigureSplitterMode()
    {
        Title = "ToolTipTest Splitter/Capture Probe";
        Width = 480;
        Height = 320;
        Left = 100;
        Top = 100;
        WindowStartupLocation = WindowStartupLocation.Manual;
        Background = Brushes.White;

        var grid = new Grid();
        // Left column is a real pixel width so DragDelta can visibly resize the panes (a bare
        // logging DragDelta looked like "拖不动" even when the input was flowing).
        var leftCol = new ColumnDefinition { Width = new GridLength(200) };
        grid.ColumnDefinitions.Add(leftCol);
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var left = new Border { Background = Brushes.LightBlue };
        Grid.SetColumn(left, 0);
        var right = new Border { Background = Brushes.LightGreen };
        Grid.SetColumn(right, 2);

        // Mimics AvalonDock.Controls.LayoutGridResizerControl: a Thumb with a resize cursor.
        splitterThumb = new Thumb
        {
            Cursor = Cursors.SizeWE,
            Background = Brushes.Gray,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };
        AutomationProperties.SetAutomationId(splitterThumb, "SplitterProbeThumb");
        Grid.SetColumn(splitterThumb, 1);

        // Control experiment: TOOLTIPTEST_SPLITTER_NO_OVERLAY=1 skips showing the separate overlay
        // Window on DragStarted. If the drag then works (moves arrive, real up only on release),
        // the overlay Window.Show() is conclusively the cause of the phantom-MouseUp-kills-drag bug.
        var skipOverlay = Environment.GetEnvironmentVariable("TOOLTIPTEST_SPLITTER_NO_OVERLAY") == "1";
        splitterThumb.DragStarted += (s, e) =>
        {
            splitterDragStarted++;
            splitterHorizontalChange = 0;
            splitterInitialWidth = leftCol.ActualWidth;
            Log($"SPLIT DragStarted #{splitterDragStarted} captured={CapturedName()} isThumb={ReferenceEquals(Mouse.Captured, splitterThumb)} skipOverlay={skipOverlay} usePopup={splitterUsePopup}");
            if (skipOverlay)
                return;
            ShowSplitterOverlay();
            Log($"SPLIT afterOverlayShow captured={CapturedName()} isThumb={ReferenceEquals(Mouse.Captured, splitterThumb)} overlayAlive={splitterOverlay != null || splitterPopup != null || splitterAdorner != null}");
        };
        splitterThumb.DragDelta += (s, e) =>
        {
            splitterDragDelta++;
            // Thumb reports displacement from the drag origin, not an event-to-event increment.
            splitterHorizontalChange = e.HorizontalChange;
            MoveSplitterOverlay(splitterHorizontalChange);
            if (splitterDragDelta <= 5 || splitterDragDelta % 20 == 0)
                Log($"SPLIT DragDelta #{splitterDragDelta} h={e.HorizontalChange:0.0} captured={CapturedName()}");
        };
        splitterThumb.DragCompleted += (s, e) =>
        {
            splitterDragCompleted++;
            // AvalonDock commits the ghost position, which is driven by DragDelta in layout DIPs.
            // DragCompleted can be in screen pixels on a Retina portable source.
            var newWidth = Math.Max(20, splitterInitialWidth + splitterHorizontalChange);
            leftCol.Width = new GridLength(newWidth);
            Log($"SPLIT DragCompleted #{splitterDragCompleted} h={e.HorizontalChange:0.0} latestDelta={splitterHorizontalChange:0.0} newLeftW={newWidth:0.0} captured={CapturedName()}");
            HideSplitterOverlay();
            var passed = splitterDragStarted == 1 && splitterDragDelta > 0 &&
                splitterHorizontalChange > 10 &&
                Math.Abs((newWidth - splitterInitialWidth) - splitterHorizontalChange) <= 2;
            Log($"SPLIT_RESULT: {(passed ? "PASS" : "FAIL")} started={splitterDragStarted} deltas={splitterDragDelta} completed={splitterDragCompleted} lostCapture={splitterLostCapture} initialWidth={splitterInitialWidth:0.0} finalWidth={newWidth:0.0} change={e.HorizontalChange:0.0}");
        };
        splitterThumb.LostMouseCapture += (s, e) =>
        {
            splitterLostCapture++;
            Log($"SPLIT LostMouseCapture #{splitterLostCapture} nowCaptured={CapturedName()}");
        };
        splitterThumb.QueryCursor += (s, e) =>
            Log($"SPLIT QueryCursor cursor={e.Cursor} over={e.OriginalSource?.GetType().Name} captured={CapturedName()}");

        grid.Children.Add(left);
        grid.Children.Add(right);
        grid.Children.Add(splitterThumb);
        splitterStatus = new TextBlock
        {
            Background = Brushes.White,
            Foreground = Brushes.Black,
            Padding = new Thickness(6),
            TextWrapping = TextWrapping.Wrap,
            IsHitTestVisible = false
        };
        var host = new DockPanel();
        DockPanel.SetDock(splitterStatus, Dock.Top);
        host.Children.Add(splitterStatus);
        host.Children.Add(new AdornerDecorator { Child = grid });
        Content = host;

        // Window-wide tunnelling trace: sees raw input before capture retargeting/handling.
        PreviewMouseDown += (s, e) =>
            Log($"WIN PreviewMouseDown pos={Fmt(e.GetPosition(this))} src={e.OriginalSource?.GetType().Name} captured={CapturedName()}");
        PreviewMouseMove += (s, e) =>
        {
            if (e.LeftButton == MouseButtonState.Pressed)
                Log($"WIN PreviewMouseMove(LDown) pos={Fmt(e.GetPosition(this))} src={e.OriginalSource?.GetType().Name} captured={CapturedName()}");
        };
        PreviewMouseUp += (s, e) =>
            Log($"WIN PreviewMouseUp pos={Fmt(e.GetPosition(this))} src={e.OriginalSource?.GetType().Name} captured={CapturedName()}");

        Loaded += (s, e) =>
        {
            Activate();
            var topLeft = splitterThumb.PointToScreen(new Point());
            Log($"SPLIT_MODE_READY x={topLeft.X:0.0} y={topLeft.Y:0.0} width={splitterThumb.ActualWidth:0.0} height={splitterThumb.ActualHeight:0.0}");
            RunSyntheticCaptureProbe();
            Activate();
        };
    }

    void MoveSplitterOverlay(double horizontalChange)
    {
        if (splitterAdorner != null)
        {
            splitterAdorner.OffsetX = horizontalChange;
            splitterAdorner.InvalidateVisual();
        }
        else if (splitterPopup != null)
        {
            splitterPopup.HorizontalOffset = splitterOverlayStartX + horizontalChange;
        }
        else if (splitterOverlay != null)
        {
            splitterOverlay.Left = splitterOverlayStartX + horizontalChange;
        }
    }

    void ShowSplitterOverlay()
    {
        var topLeft = splitterThumb!.PointToScreen(new Point(0, 0));
        splitterOverlayStartX = topLeft.X;
        double w = Math.Max(1, splitterThumb.ActualWidth);
        double h = Math.Max(1, splitterThumb.ActualHeight);
        var ghost = new Border { Background = Brushes.Black, Opacity = 0.5, Width = w, Height = h };

        if (splitterUseAdorner)
        {
            var layer = AdornerLayer.GetAdornerLayer(splitterThumb);
            if (layer == null)
            {
                Log("SPLIT adorner layer NOT FOUND (no AdornerDecorator ancestor?)");
                return;
            }
            splitterAdorner = new GhostAdorner(splitterThumb);
            layer.Add(splitterAdorner);
            return;
        }

        if (splitterUsePopup)
        {
            // Popup host: goes through WpfPortablePopupActivation → HasAnyOpenPopup suppresses the
            // main window's spurious Deactivated, so the drag isn't torn down.
            splitterPopup = new Popup
            {
                Placement = PlacementMode.Absolute,
                HorizontalOffset = topLeft.X,
                VerticalOffset = topLeft.Y,
                StaysOpen = true,
                AllowsTransparency = true,
                Child = ghost,
            };
            splitterPopup.IsOpen = true;
            return;
        }

        var canvas = new Canvas { HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch };
        canvas.Children.Add(ghost);

        // Same shape as AvalonDock's resizer overlay window.
        splitterOverlay = new Window
        {
            Style = new Style(typeof(Window), null),
            SizeToContent = SizeToContent.Manual,
            ResizeMode = ResizeMode.NoResize,
            WindowStyle = WindowStyle.None,
            ShowInTaskbar = false,
            AllowsTransparency = true,
            Background = null,
            Width = w,
            Height = h,
            Left = topLeft.X,
            Top = topLeft.Y,
            ShowActivated = false,
            Owner = null,
            Content = canvas,
        };
        splitterOverlay.Show();
        Log($"SPLIT overlay window requested Left/Top={topLeft.X:0},{topLeft.Y:0} actual={splitterOverlay.Left:0},{splitterOverlay.Top:0} size={splitterOverlay.Width:0}x{splitterOverlay.Height:0}");
    }

    void HideSplitterOverlay()
    {
        if (splitterAdorner != null)
        {
            AdornerLayer.GetAdornerLayer(splitterThumb!)?.Remove(splitterAdorner);
            splitterAdorner = null;
        }
        if (splitterPopup != null)
        {
            splitterPopup.IsOpen = false;
            splitterPopup = null;
        }
        if (splitterOverlay == null)
            return;
        splitterOverlay.Close();
        splitterOverlay = null;
    }

    // Deterministic, no-gesture probe: does managed mouse capture survive showing a separate
    // top-level Window mid-"drag"? PASS = capture held in both the control and the suspect case.
    void RunSyntheticCaptureProbe()
    {
        var host = (DockPanel)Content;
        var decorator = (AdornerDecorator)host.Children[1];
        var grid = (Grid)decorator.Child;
        var probe = new Border { Width = 10, Height = 10, Background = Brushes.Transparent };
        Grid.SetColumn(probe, 0);
        grid.Children.Add(probe);
        UpdateLayout();

        // A: capture with no extra window (control).
        bool capA = probe.CaptureMouse();
        bool heldA = ReferenceEquals(Mouse.Captured, probe);
        Log($"PROBE A (capture, no window): CaptureMouse()={capA} held={heldA} captured={CapturedName()}");
        probe.ReleaseMouseCapture();

        // B: capture, then show a separate transparent top-level window (the suspect step).
        bool capB = probe.CaptureMouse();
        bool heldBefore = ReferenceEquals(Mouse.Captured, probe);
        var w = new Window
        {
            Style = new Style(typeof(Window), null),
            WindowStyle = WindowStyle.None,
            AllowsTransparency = true,
            Background = null,
            ShowActivated = false,
            ShowInTaskbar = false,
            ResizeMode = ResizeMode.NoResize,
            Width = 40,
            Height = 40,
            Left = Left + 220,
            Top = Top + 120,
        };
        w.Show();
        bool heldAfter = ReferenceEquals(Mouse.Captured, probe);
        Log($"PROBE B (capture, then Window.Show()): CaptureMouse()={capB} heldBefore={heldBefore} heldAfterShow={heldAfter} captured={CapturedName()}");
        w.Close();
        bool heldAfterClose = ReferenceEquals(Mouse.Captured, probe);
        Log($"PROBE B afterClose: held={heldAfterClose} captured={CapturedName()}");
        probe.ReleaseMouseCapture();
        grid.Children.Remove(probe);

        bool pass = heldA && heldAfter;
        Log($"CAPTURE_PROBE: {(pass ? "PASS" : "FAIL")} heldWithoutWindow={heldA} heldAfterShow={heldAfter} heldAfterClose={heldAfterClose}");
    }

    void LogImageProbeReady()
    {
        if (bitmapImageProbe == null || vectorImageProbe == null || drawingBrushProbe == null)
            return;

        Point bitmapTopLeft = bitmapImageProbe.PointToScreen(new Point(0, 0));
        Point imageTopLeft = vectorImageProbe.PointToScreen(new Point(0, 0));
        Point brushTopLeft = drawingBrushProbe.PointToScreen(new Point(0, 0));
        Log("IMAGE_TEST_READY" +
            $" bitmap={bitmapTopLeft.X:0},{bitmapTopLeft.Y:0},{bitmapImageProbe.ActualWidth:0},{bitmapImageProbe.ActualHeight:0}" +
            $" image={imageTopLeft.X:0},{imageTopLeft.Y:0},{vectorImageProbe.ActualWidth:0},{vectorImageProbe.ActualHeight:0}" +
            $" brush={brushTopLeft.X:0},{brushTopLeft.Y:0},{drawingBrushProbe.ActualWidth:0},{drawingBrushProbe.ActualHeight:0}");
    }

    void StartHoverModeVerificationTimer()
    {
        var verifyTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(18)
        };
        verifyTimer.Tick += (ts, te) =>
        {
            verifyTimer.Stop();
            isVerifying = true;
            bool pass = nestedSubmenuOpened &&
                lazySubmenuRebuilt &&
                !submenuPrematureClose &&
                !nestedSubmenuPrematureClose;
            Log("RESULT: " + (pass ? "PASS" : "FAIL") +
                " mode=hover" +
                " nestedSubmenuOpened=" + nestedSubmenuOpened +
                " lazySubmenuRebuilt=" + lazySubmenuRebuilt +
                " submenuPrematureClose=" + submenuPrematureClose +
                " nestedSubmenuPrematureClose=" + nestedSubmenuPrematureClose);
            Application.Current.Shutdown();
        };
        verifyTimer.Start();
    }

    void Log(string message)
    {
        var rendered = DateTime.Now.ToString("HH:mm:ss.fff") + " " + message;
        if (splitterStatus != null)
            splitterStatus.Text = rendered;
        else
            StatusText.Text = rendered;
        Console.WriteLine("[tooltiptest] " + message);
        try
        {
            System.IO.File.AppendAllText("/tmp/tooltiptest_debug.log", DateTime.Now.ToString("HH:mm:ss.fff") + " [app] " + message + "\n");
        }
        catch { }
    }

    void DumpToolTipTree(ToolTip toolTip, string name)
    {
        toolTip.ApplyTemplate();
        Log($"{name}: IsOpen={toolTip.IsOpen}, Content={toolTip.Content}, " +
            $"Actual={toolTip.ActualWidth}x{toolTip.ActualHeight}, " +
            $"Foreground={toolTip.Foreground}, Background={toolTip.Background}, " +
            $"Template={(toolTip.Template == null ? "null" : toolTip.Template.GetType().Name)}");
        DumpVisualTree(toolTip, name, 0);
    }

    void DumpVisualTree(DependencyObject node, string name, int depth)
    {
        if (depth > 8)
            return;

        Log($"{name}: tree depth={depth} type={node.GetType().FullName} " +
            (node is FrameworkElement fe ? $"actual={fe.ActualWidth}x{fe.ActualHeight} desired={fe.DesiredSize}" : ""));

        int count;
        try { count = VisualTreeHelper.GetChildrenCount(node); }
        catch (Exception ex) { Log($"{name}: tree error={ex.GetType().Name}: {ex.Message}"); return; }

        for (int i = 0; i < count; i++)
            DumpVisualTree(VisualTreeHelper.GetChild(node, i), name, depth + 1);
    }

    void OnForceShowClick(object sender, RoutedEventArgs e)
    {
        object? tooltip = ToolTipService.GetToolTip(PlainButton);
        Log("GetToolTip returned: " + (tooltip?.GetType().Name ?? "null") + " value=" + tooltip);

        if (tooltip is ToolTip tt)
        {
            tt.PlacementTarget = PlainButton;
            tt.IsOpen = false;
            tt.IsOpen = true;
            Log("Set ToolTip.IsOpen = true; IsOpen is now " + tt.IsOpen);
        }
        else if (tooltip is string s)
        {
            var wrapper = new ToolTip { Content = s, PlacementTarget = PlainButton, IsOpen = true };
            Log("Wrapped plain string into ToolTip; IsOpen=" + wrapper.IsOpen);
        }
    }

    void OnForcePopupClick(object sender, RoutedEventArgs e)
    {
        Point expectedTopLeft = ForcePopupButton.PointToScreen(new Point(0, ForcePopupButton.ActualHeight));
        Log($"Expected Bottom-placement top-left (via PointToScreen): {expectedTopLeft.X}, {expectedTopLeft.Y}");

        if (rawPopup != null)
            rawPopup.IsOpen = false;
        rawPopup = new Popup
        {
            PlacementTarget = ForcePopupButton,
            Placement = PlacementMode.Bottom,
            StaysOpen = true,
            Child = new Border
            {
                Background = System.Windows.Media.Brushes.Yellow,
                BorderBrush = System.Windows.Media.Brushes.Black,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(8),
                Child = new TextBlock { Text = "Raw Popup content" }
            }
        };
        rawPopup.IsOpen = true;
        Log("Raw Popup.IsOpen set to true; actual IsOpen=" + rawPopup.IsOpen);
    }

    void OnFileSubmenuOpened(object sender, RoutedEventArgs e)
    {
        Point screenPoint = FileMenuItem.PointToScreen(new Point(0, 0));
        Log("Main File menu SubmenuOpened fired; IsSubmenuOpen=" + FileMenuItem.IsSubmenuOpen +
            $" targetScreen={screenPoint.X},{screenPoint.Y}");
    }

    void OnFileSubmenuClosed(object sender, RoutedEventArgs e)
    {
        if (!expectingSubmenuClose && !isVerifying)
        {
            submenuPrematureClose = true;
            Log("Main File menu SubmenuClosed fired UNEXPECTEDLY (premature dismissal)");
        }
        else
        {
            Log("Main File menu SubmenuClosed fired (expected)");
        }
    }

    void OnNewSubmenuOpened(object sender, RoutedEventArgs e)
    {
        Log("File > New SubmenuOpened fired; IsSubmenuOpen=" + NewMenuItem.IsSubmenuOpen);
    }

    void OnNewSubmenuClosed(object sender, RoutedEventArgs e)
    {
        Log("File > New SubmenuClosed fired");
    }

    void OnRecentFilesSubmenuOpened(object sender, RoutedEventArgs e)
    {
        // SubmenuOpened is a bubbling routed event, so a descendant submenu opening (e.g. the
        // nested Workspaces flyout) also raises this handler. Only rebuild when *this* menu item's
        // own submenu opened - rebuilding on a descendant's open would recreate the very item the
        // user just hovered, on every open, in an endless loop (real WPF menu services guard the
        // same way).
        if (!ReferenceEquals(e.OriginalSource, RecentFilesMenuItem))
            return;

        nestedSubmenuOpened = true;
        RebuildRecentFilesSubmenu();
        Point screenPoint = RecentFilesMenuItem.PointToScreen(new Point(0, 0));
        Log("File > Recent Files SubmenuOpened fired; IsSubmenuOpen=" + RecentFilesMenuItem.IsSubmenuOpen +
            $" targetScreen={screenPoint.X},{screenPoint.Y}");
    }

    void RebuildRecentFilesSubmenu()
    {
        // Matches OpenDevelop's MenuService.ReplaceMenuItems() pattern: a submenu
        // opens with a dummy child, then clears and inserts real menu items.
        RecentFilesMenuItem.Items.Clear();
        RecentFilesMenuItem.Items.Add(new MenuItem { Header = "Sample.xaml" });
        RecentFilesMenuItem.Items.Add(new MenuItem { Header = "PopupNotes.txt" });
        RecentFilesMenuItem.Items.Add(new Separator());

        recentWorkspacesMenuItem = new MenuItem { Header = "_Workspaces" };
        AutomationProperties.SetAutomationId(recentWorkspacesMenuItem, "RecentWorkspacesMenuItem");
        recentWorkspacesMenuItem.SubmenuOpened += OnRecentWorkspacesSubmenuOpened;
        recentWorkspacesMenuItem.SubmenuClosed += OnRecentWorkspacesSubmenuClosed;
        recentWorkspacesMenuItem.Items.Add(new MenuItem { Header = "LibreWPF Debugging" });
        recentWorkspacesMenuItem.Items.Add(new MenuItem { Header = "OpenDevelop Layout" });
        RecentFilesMenuItem.Items.Add(recentWorkspacesMenuItem);

        lazySubmenuRebuilt = true;
        Log("File > Recent Files lazy submenu rebuilt; childCount=" + RecentFilesMenuItem.Items.Count);
    }

    void OnRecentFilesSubmenuClosed(object sender, RoutedEventArgs e)
    {
        if (!expectingSubmenuClose && !isVerifying)
        {
            nestedSubmenuPrematureClose = true;
            Log("File > Recent Files SubmenuClosed fired UNEXPECTEDLY (premature dismissal)");
        }
        else
        {
            Log("File > Recent Files SubmenuClosed fired (expected)");
        }
    }

    void OnRecentWorkspacesSubmenuOpened(object sender, RoutedEventArgs e)
    {
        nestedSubmenuOpened = true;
        if (recentWorkspacesMenuItem == null)
        {
            Log("File > Recent Files > Workspaces SubmenuOpened fired without a tracked menu item");
            return;
        }

        Point screenPoint = recentWorkspacesMenuItem.PointToScreen(new Point(0, 0));
        Log("File > Recent Files > Workspaces SubmenuOpened fired; IsSubmenuOpen=" +
            recentWorkspacesMenuItem.IsSubmenuOpen +
            $" targetScreen={screenPoint.X},{screenPoint.Y}");
    }

    void OnRecentWorkspacesSubmenuClosed(object sender, RoutedEventArgs e)
    {
        if (!expectingSubmenuClose && !isVerifying)
        {
            nestedSubmenuPrematureClose = true;
            Log("File > Recent Files > Workspaces SubmenuClosed fired UNEXPECTEDLY (premature dismissal)");
        }
        else
        {
            Log("File > Recent Files > Workspaces SubmenuClosed fired (expected)");
        }
    }

    void OnToolbarComboBoxDropDownOpened(object sender, EventArgs e)
    {
        Point screenPoint = ToolbarComboBox.PointToScreen(new Point(0, 0));
        Log("Toolbar ComboBox DropDownOpened fired; IsDropDownOpen=" + ToolbarComboBox.IsDropDownOpen +
            $" targetScreen={screenPoint.X},{screenPoint.Y}");
    }

    void OnToolbarComboBoxDropDownClosed(object sender, EventArgs e)
    {
        // Nothing in this test intentionally closes the ComboBox dropdown before
        // verification runs, so a close event observed before then means it was
        // dismissed prematurely (e.g. by a spurious host-window move releasing mouse
        // capture). Once isVerifying is set, app-shutdown window teardown itself
        // closes the dropdown - that's not a bug.
        if (!isVerifying)
        {
            comboPrematureClose = true;
            Log("Toolbar ComboBox DropDownClosed fired UNEXPECTEDLY (premature dismissal)");
        }
        else
        {
            Log("Toolbar ComboBox DropDownClosed fired (shutdown teardown)");
        }
    }
}

// Draws a translucent resize ghost directly over the adorned splitter, in the main window's
// AdornerLayer — no separate native window. This is the shape the AvalonDock fix would take.
sealed class GhostAdorner : Adorner
{
    readonly Brush _fill = new SolidColorBrush(Color.FromArgb(128, 0, 0, 0));
    public double OffsetX { get; set; }

    public GhostAdorner(UIElement adornedElement) : base(adornedElement)
    {
        IsHitTestVisible = false;
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        var size = AdornedElement.RenderSize;
        drawingContext.PushTransform(new TranslateTransform(OffsetX, 0));
        drawingContext.DrawRectangle(_fill, null, new Rect(0, 0, size.Width, size.Height));
        drawingContext.Pop();
    }
}

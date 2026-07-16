using System;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Documents;

namespace ToolTipTest;

class SplitterCaptureTest : TestCase
{
    Thumb? splitterThumb;
    int splitterDragStarted, splitterDragDelta, splitterDragCompleted, splitterLostCapture;
    double splitterHorizontalChange, splitterInitialWidth, splitterOverlayStartX;
    ColumnDefinition? leftCol;
    Window? splitterOverlay;
    Popup? splitterPopup;
    GhostAdorner? splitterAdorner;
    TextBlock? splitterStatus;

    public SplitterCaptureTest()
    {
        Name = "Splitter Capture";
        Description = "Tests Thumb drag with overlay window and mouse capture survival";
    }

    public override void Setup()
    {

        var grid = new Grid();
        leftCol = new ColumnDefinition { Width = new GridLength(200) };
        grid.ColumnDefinitions.Add(leftCol);
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var left = new Border { Background = Brushes.LightBlue };
        Grid.SetColumn(left, 0);
        var right = new Border { Background = Brushes.LightGreen };
        Grid.SetColumn(right, 2);

        splitterThumb = new Thumb { Cursor = Cursors.SizeWE, Background = Brushes.Gray, HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch };
        AutomationProperties.SetAutomationId(splitterThumb, "SplitterProbeThumb");
        Grid.SetColumn(splitterThumb, 1);

        splitterThumb.DragStarted += (s, e) =>
        {
            splitterDragStarted++;
            splitterHorizontalChange = 0;
            if (leftCol != null) splitterInitialWidth = leftCol.ActualWidth;
            Log($"SPLIT DragStarted #{splitterDragStarted} captured={CapturedName()} isThumb={ReferenceEquals(Mouse.Captured, splitterThumb)}");
            ShowSplitterOverlay();
            Log($"SPLIT afterOverlayShow captured={CapturedName()} isThumb={ReferenceEquals(Mouse.Captured, splitterThumb)} overlayAlive={splitterOverlay != null || splitterPopup != null || splitterAdorner != null}");
        };
        splitterThumb.DragDelta += (s, e) =>
        {
            splitterDragDelta++;
            splitterHorizontalChange = e.HorizontalChange;
            MoveSplitterOverlay(splitterHorizontalChange);
            if (splitterDragDelta <= 5 || splitterDragDelta % 20 == 0)
                Log($"SPLIT DragDelta #{splitterDragDelta} h={e.HorizontalChange:0.0} captured={CapturedName()}");
        };
        splitterThumb.DragCompleted += (s, e) =>
        {
            splitterDragCompleted++;
            var newWidth = Math.Max(20, splitterInitialWidth + splitterHorizontalChange);
            if (leftCol != null) leftCol.Width = new GridLength(newWidth);
            Log($"SPLIT DragCompleted #{splitterDragCompleted} h={e.HorizontalChange:0.0} latestDelta={splitterHorizontalChange:0.0} newLeftW={newWidth:0.0} captured={CapturedName()}");
            HideSplitterOverlay();
            var passed = splitterDragStarted == 1 && splitterDragDelta > 0 && splitterHorizontalChange > 10 && Math.Abs((newWidth - splitterInitialWidth) - splitterHorizontalChange) <= 2;
            Log($"SPLIT_RESULT: {(passed ? "PASS" : "FAIL")} started={splitterDragStarted} deltas={splitterDragDelta} completed={splitterDragCompleted} lostCapture={splitterLostCapture} initialWidth={splitterInitialWidth:0.0} finalWidth={newWidth:0.0} change={e.HorizontalChange:0.0}");
            SetResult(passed);
        };
        splitterThumb.LostMouseCapture += (s, e) => { splitterLostCapture++; Log($"SPLIT LostMouseCapture #{splitterLostCapture} nowCaptured={CapturedName()}"); };

        grid.Children.Add(left);
        grid.Children.Add(right);
        grid.Children.Add(splitterThumb);
        splitterStatus = new TextBlock { Background = Brushes.White, Foreground = Brushes.Black, Padding = new Thickness(6), TextWrapping = TextWrapping.Wrap, IsHitTestVisible = false };
        var host = new DockPanel();
        DockPanel.SetDock(splitterStatus, Dock.Top);
        host.Children.Add(splitterStatus);
        host.Children.Add(new AdornerDecorator { Child = grid });
        ContentRoot = host;
    }

    public override void Run()
    {
        CreateContentWindow("ToolTipTest Splitter/Capture Probe", 480, 320);

        if (TestWindow != null)
        {
            TestWindow.PreviewMouseDown += (s, e) => Log($"WIN PreviewMouseDown pos={Fmt(e.GetPosition(TestWindow))} src={e.OriginalSource?.GetType().Name} captured={CapturedName()}");
            TestWindow.PreviewMouseMove += (s, e) => { if (e.LeftButton == MouseButtonState.Pressed) Log($"WIN PreviewMouseMove(LDown) pos={Fmt(e.GetPosition(TestWindow))} src={e.OriginalSource?.GetType().Name} captured={CapturedName()}"); };
            TestWindow.PreviewMouseUp += (s, e) => Log($"WIN PreviewMouseUp pos={Fmt(e.GetPosition(TestWindow))} src={e.OriginalSource?.GetType().Name} captured={CapturedName()}");
        }

        if (splitterThumb != null)
        {
            var topLeft = splitterThumb.PointToScreen(new Point());
            Log($"SPLIT_MODE_READY x={topLeft.X:0.0} y={topLeft.Y:0.0} width={splitterThumb.ActualWidth:0.0} height={splitterThumb.ActualHeight:0.0}");
        }
        RunSyntheticCaptureProbe();
    }

    public override void Cleanup() { HideSplitterOverlay(); }

    void MoveSplitterOverlay(double horizontalChange)
    {
        if (splitterAdorner != null) { splitterAdorner.OffsetX = horizontalChange; splitterAdorner.InvalidateVisual(); }
        else if (splitterPopup != null) splitterPopup.HorizontalOffset = splitterOverlayStartX + horizontalChange;
        else if (splitterOverlay != null) splitterOverlay.Left = splitterOverlayStartX + horizontalChange;
    }

    void ShowSplitterOverlay()
    {
        if (splitterThumb == null) return;
        var topLeft = splitterThumb.PointToScreen(new Point(0, 0));
        splitterOverlayStartX = topLeft.X;
        var w = Math.Max(1, splitterThumb.ActualWidth);
        var h = Math.Max(1, splitterThumb.ActualHeight);
        var ghost = new Border { Background = Brushes.Black, Opacity = 0.5, Width = w, Height = h };

        if (Environment.GetEnvironmentVariable("TOOLTIPTEST_SPLITTER_ADORNER") == "1")
        {
            var layer = AdornerLayer.GetAdornerLayer(splitterThumb);
            if (layer == null) { Log("SPLIT adorner layer NOT FOUND"); return; }
            splitterAdorner = new GhostAdorner(splitterThumb);
            layer.Add(splitterAdorner);
            return;
        }

        if (Environment.GetEnvironmentVariable("TOOLTIPTEST_SPLITTER_POPUP") == "1")
        {
            splitterPopup = new Popup { Placement = PlacementMode.Absolute, HorizontalOffset = topLeft.X, VerticalOffset = topLeft.Y, StaysOpen = true, AllowsTransparency = true, Child = ghost };
            splitterPopup.IsOpen = true;
            return;
        }

        var canvas = new Canvas();
        canvas.Children.Add(ghost);
        splitterOverlay = new Window
        {
            Style = new Style(typeof(Window), null), SizeToContent = SizeToContent.Manual, ResizeMode = ResizeMode.NoResize,
            WindowStyle = WindowStyle.None, ShowInTaskbar = false, AllowsTransparency = true, Background = null,
            Width = w, Height = h, Left = topLeft.X, Top = topLeft.Y, ShowActivated = false, Owner = null, Content = canvas
        };
        splitterOverlay.Show();
        Log($"SPLIT overlay window requested Left/Top={topLeft.X:0},{topLeft.Y:0} actual={splitterOverlay.Left:0},{splitterOverlay.Top:0} size={splitterOverlay.Width:0}x{splitterOverlay.Height:0}");
    }

    void HideSplitterOverlay()
    {
        if (splitterAdorner != null) { if (splitterThumb != null) AdornerLayer.GetAdornerLayer(splitterThumb)?.Remove(splitterAdorner); splitterAdorner = null; }
        if (splitterPopup != null) { splitterPopup.IsOpen = false; splitterPopup = null; }
        if (splitterOverlay == null) return;
        splitterOverlay.Close();
        splitterOverlay = null;
    }

    void RunSyntheticCaptureProbe()
    {
        var host = ContentRoot as DockPanel;
        if (host == null) return;
        var decorator = host.Children[1] as AdornerDecorator;
        var grid = decorator?.Child as Grid;
        if (grid == null) return;

        var probe = new Border { Width = 10, Height = 10, Background = Brushes.Transparent };
        Grid.SetColumn(probe, 0);
        grid.Children.Add(probe);
        TestWindow?.UpdateLayout();

        bool capA = probe.CaptureMouse();
        bool heldA = ReferenceEquals(Mouse.Captured, probe);
        Log($"PROBE A (capture, no window): CaptureMouse()={capA} held={heldA} captured={CapturedName()}");
        probe.ReleaseMouseCapture();

        bool capB = probe.CaptureMouse();
        bool heldBefore = ReferenceEquals(Mouse.Captured, probe);
        var w = new Window { Style = new Style(typeof(Window), null), WindowStyle = WindowStyle.None, AllowsTransparency = true, Background = null, ShowActivated = false, ShowInTaskbar = false, ResizeMode = ResizeMode.NoResize, Width = 40, Height = 40, Left = (TestWindow?.Left ?? 0) + 220, Top = (TestWindow?.Top ?? 0) + 120 };
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
}

sealed class GhostAdorner : Adorner
{
    readonly Brush fill = new SolidColorBrush(Color.FromArgb(128, 0, 0, 0));
    public double OffsetX { get; set; }
    public GhostAdorner(UIElement adornedElement) : base(adornedElement) { IsHitTestVisible = false; }
    protected override void OnRender(DrawingContext drawingContext)
    {
        var size = AdornedElement.RenderSize;
        drawingContext.PushTransform(new TranslateTransform(OffsetX, 0));
        drawingContext.DrawRectangle(fill, null, new Rect(0, 0, size.Width, size.Height));
        drawingContext.Pop();
    }
}

using System;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using AvalonDock;
using AvalonDock.Controls;
using AvalonDock.Layout;

namespace ToolTipTest;

class AvalonDockFloatTest : TestCase
{
    public AvalonDockFloatTest()
    {
        Name = "AvalonDock Float";
        Description = "Tests tearing a docked tool pane out into a floating window via drag";
    }

    public override void Setup()
    {
        var tool = new LayoutAnchorable
        {
            Title = "Float Drag Tool",
            ContentId = "floatDragTool",
            CanClose = false,
            CanHide = true,
            Content = new TextBlock { Text = "Float Drag Tool", HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }
        };
        var toolPane = new LayoutAnchorablePane(tool) { DockWidth = new GridLength(240) };
        var documentPane = new LayoutDocumentPane();
        documentPane.Children.Add(new LayoutDocument { Title = "Float Drag Document", ContentId = "floatDragDocument", Content = new Grid { Background = Brushes.White } });
        var manager = new DockingManager
        {
            Layout = new LayoutRoot { RootPanel = new LayoutPanel { Orientation = Orientation.Horizontal, Children = { toolPane, documentPane } } }
        };
        AutomationProperties.SetAutomationId(manager, "FloatDragManager");

        ContentRoot = manager;
    }

    public override void Run()
    {
        CreateContentWindow("ToolTipTest AvalonDock Float Drag Probe", 800, 560);

        if (TestWindow != null)
        {
            TestWindow.PreviewMouseDown += (s, e) => Log($"FLOAT PreviewDown pos={Fmt(e.GetPosition(TestWindow))} src={e.OriginalSource?.GetType().Name} captured={CapturedName()}");
            TestWindow.PreviewMouseMove += (s, e) => { if (e.LeftButton == MouseButtonState.Pressed) Log($"FLOAT PreviewMove pos={Fmt(e.GetPosition(TestWindow))} src={e.OriginalSource?.GetType().Name} captured={CapturedName()}"); };
            TestWindow.PreviewMouseUp += (s, e) => Log($"FLOAT PreviewUp pos={Fmt(e.GetPosition(TestWindow))} src={e.OriginalSource?.GetType().Name} captured={CapturedName()}");
        }

        System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(new Action(() =>
        {
            var manager = ContentRoot as DockingManager;
            if (manager == null) { SetResult(false); return; }
            var tool = manager.Layout?.RootPanel?.Children
                .OfType<LayoutAnchorablePane>().FirstOrDefault()
                ?.Children.FirstOrDefault();
            if (tool == null) { Log("FLOAT_RESULT: FAIL reason=tool-not-found"); SetResult(false); return; }

            manager.UpdateLayout();
            var title = FindVisualDescendant<AnchorablePaneTitle>(manager, x => ReferenceEquals(x.Model, tool));
            if (title == null) { Log("FLOAT_RESULT: FAIL reason=title-not-found"); SetResult(false); return; }
            AutomationProperties.SetAutomationId(title, "FloatDragTitle");
            var topLeft = title.PointToScreen(new Point());
            Log($"FLOAT_MODE_READY x={topLeft.X:0.0} y={topLeft.Y:0.0} width={title.ActualWidth:0.0} height={title.ActualHeight:0.0}");

            System.Windows.Threading.DispatcherTimer? timer = null;
            title.PreviewMouseDown += (ts, te) =>
            {
                if (timer != null) return;
                var ticks = 0;
                timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
                timer.Tick += (timerSender, timerArgs) =>
                {
                    ticks++;
                    if (manager.FloatingWindows.Any())
                    {
                        var floating = manager.FloatingWindows.First();
                        var screenOrigin = floating.PointToScreen(new Point());
                        var source = PresentationSource.FromVisual(floating);
                        var toDevice = source?.CompositionTarget?.TransformToDevice ?? Matrix.Identity;
                        var sourceReady = floating.IsVisible && source != null && (Math.Abs(floating.Left) > 0.1 || Math.Abs(floating.Top) > 0.1);
                        if (!sourceReady && ticks < 80) return;
                        var coordinatesMatch = Math.Abs(screenOrigin.X - floating.Left) <= 2 && Math.Abs(screenOrigin.Y - floating.Top) <= 2;
                        timer.Stop();
                        var fwCount = manager.FloatingWindows.Count();
                        Log($"FLOAT_COORDINATES window={floating.Left:0.0},{floating.Top:0.0} pointToScreen={screenOrigin.X:0.0},{screenOrigin.Y:0.0} compatibilityTargetScale={toDevice.M11:0.0},{toDevice.M22:0.0}");
                        Log($"FLOAT_RESULT: {(coordinatesMatch ? "PASS" : "FAIL")} floatingWindows={fwCount} isFloating={tool.IsFloating} coordinatesMatch={coordinatesMatch}");
                        SetResult(coordinatesMatch);
                    }
                    else if (ticks >= 80) { timer.Stop(); var fwCount = manager.FloatingWindows.Count(); Log($"FLOAT_RESULT: FAIL floatingWindows={fwCount} isFloating={tool.IsFloating}"); SetResult(false); }
                };
                timer.Start();
            };
        }));
    }
}

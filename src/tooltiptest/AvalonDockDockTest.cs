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

class AvalonDockDockTest : TestCase
{
    LayoutDocumentPane? dockDocumentPane;
    LayoutAnchorable? dockTool;
    DockingManager? dockManager;
    Grid? dockDocumentContent;

    public AvalonDockDockTest()
    {
        Name = "AvalonDock Dock";
        Description = "Tests re-docking a floating tool window by dragging over drop targets";
    }

    public override void Setup()
    {
        dockTool = new LayoutAnchorable
        {
            Title = "Dock Tool",
            ContentId = "dockTool",
            CanClose = false,
            CanHide = true,
            Content = new TextBlock { Text = "Dock Tool", HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }
        };
        var toolPane = new LayoutAnchorablePane(dockTool) { DockWidth = new GridLength(220) };
        dockDocumentContent = new Grid { Background = Brushes.White };
        dockDocumentPane = new LayoutDocumentPane();
        dockDocumentPane.Children.Add(new LayoutDocument { Title = "Dock Document", ContentId = "dockDocument", Content = dockDocumentContent });
        dockManager = new DockingManager
        {
            Layout = new LayoutRoot { RootPanel = new LayoutPanel { Orientation = Orientation.Horizontal, Children = { toolPane, dockDocumentPane } } }
        };
        AutomationProperties.SetAutomationId(dockManager, "DockManager");

        ContentRoot = dockManager;
    }

    public override void Run()
    {
        CreateContentWindow("ToolTipTest AvalonDock Re-Dock Probe", 900, 620);

        if (TestWindow != null)
        {
            TestWindow.PreviewMouseDown += (s, e) => Log($"DOCK PreviewDown pos={Fmt(e.GetPosition(TestWindow))} src={e.OriginalSource?.GetType().Name} captured={CapturedName()}");
            TestWindow.PreviewMouseMove += (s, e) => { if (e.LeftButton == MouseButtonState.Pressed) Log($"DOCK PreviewMove pos={Fmt(e.GetPosition(TestWindow))} src={e.OriginalSource?.GetType().Name} captured={CapturedName()}"); };
            TestWindow.PreviewMouseUp += (s, e) => Log($"DOCK PreviewUp pos={Fmt(e.GetPosition(TestWindow))} src={e.OriginalSource?.GetType().Name} captured={CapturedName()}");
        }

        System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(new Action(() =>
        {
            dockManager?.UpdateLayout();
            dockTool?.Float();
            var settle = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(800) };
            settle.Tick += (ts, te) => { settle.Stop(); SetupDockDrop(); };
            settle.Start();
        }), System.Windows.Threading.DispatcherPriority.Loaded);
    }

    void SetupDockDrop()
    {
        if (dockManager == null || dockTool == null || dockDocumentPane == null)
        {
            Log("DOCK_RESULT: FAIL reason=setup-null");
            SetResult(false);
            return;
        }

        dockManager.UpdateLayout();
        var floating = dockManager.FloatingWindows.FirstOrDefault();
        if (floating == null)
        {
            Log($"DOCK_RESULT: FAIL reason=no-floating-window isFloating={dockTool.IsFloating}");
            SetResult(false);
            return;
        }

        floating.Left = 1080;
        floating.Top = 220;
        floating.Topmost = true;
        floating.UpdateLayout();

        FrameworkElement? dragHandle = FindVisualDescendant<AnchorablePaneTitle>(floating, _ => true) as FrameworkElement
            ?? FindVisualDescendant<DropDownControlArea>(floating, _ => true) as FrameworkElement;
        if (dragHandle == null)
        {
            var types = new System.Collections.Generic.List<string>();
            DumpDescendantTypes(floating, types, 0);
            Log("DOCK diag descendants: " + string.Join(", ", types.Distinct().Take(40)));
            Log("DOCK_RESULT: FAIL reason=drag-handle-not-found");
            SetResult(false);
            return;
        }
        AutomationProperties.SetAutomationId(dragHandle, "DockDragTitle");

        var paneControl = (FindVisualDescendant<LayoutDocumentPaneControl>(dockManager, _ => true) as FrameworkElement) ?? dockManager;
        var content = (FrameworkElement?)dockDocumentContent ?? paneControl;
        var paneCenter = paneControl.PointToScreen(new Point(paneControl.ActualWidth / 2d, paneControl.ActualHeight / 2d));
        var contentCenter = content.PointToScreen(new Point(content.ActualWidth / 2d, content.ActualHeight / 2d));
        var docCenterScreen = new Point(paneCenter.X, contentCenter.Y);
        var handleScreen = dragHandle.PointToScreen(new Point(dragHandle.ActualWidth / 2d, dragHandle.ActualHeight / 2d));

        Log($"DOCK_MODE_READY handleX={handleScreen.X:0.0} handleY={handleScreen.Y:0.0} targetX={docCenterScreen.X:0.0} targetY={docCenterScreen.Y:0.0} floatingWindows={dockManager.FloatingWindows.Count()} isFloating={dockTool.IsFloating}");

        var ticks = 0;
        var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        timer.Tick += (s, e) =>
        {
            ticks++;
            var docked = !dockTool.IsFloating && !dockManager.FloatingWindows.Any();
            var fwCount = dockManager.FloatingWindows.Count();
            if (docked) { timer.Stop(); Log($"DOCK_RESULT: PASS floatingWindows={fwCount} isFloating={dockTool.IsFloating}"); SetResult(true); }
            else if (ticks >= 80) { timer.Stop(); Log($"DOCK_RESULT: FAIL floatingWindows={fwCount} isFloating={dockTool.IsFloating}"); SetResult(false); }
        };
        timer.Start();
    }
}

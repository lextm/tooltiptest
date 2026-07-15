using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Automation;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Shapes;
using AvalonDock;
using AvalonDock.Controls;
using AvalonDock.Layout;

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
    readonly bool avalonDockFloatMode = Environment.GetEnvironmentVariable("TOOLTIPTEST_AVALONDOCK_FLOAT_MODE") == "1";
    readonly bool avalonDockDockMode = Environment.GetEnvironmentVariable("TOOLTIPTEST_AVALONDOCK_DOCK_MODE") == "1";
    readonly bool comboPopupMode = Environment.GetEnvironmentVariable("TOOLTIPTEST_COMBO_POPUP_MODE") == "1";
    Image? bitmapImageProbe;
    Image? vectorImageProbe;
    Rectangle? drawingBrushProbe;

    public MainWindow()
    {
        InitializeComponent();

        if (avalonDockFloatMode)
        {
            ConfigureAvalonDockFloatMode();
            return;
        }

        if (avalonDockDockMode)
        {
            ConfigureAvalonDockDockMode();
            return;
        }

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

        if (comboPopupMode)
        {
            ConfigureComboPopupMode();
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

    void ConfigureAvalonDockFloatMode()
    {
        Title = "ToolTipTest AvalonDock Float Drag Probe";
        Width = 800;
        Height = 560;
        Left = 100;
        Top = 100;
        WindowStartupLocation = WindowStartupLocation.Manual;

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
        documentPane.Children.Add(new LayoutDocument
        {
            Title = "Float Drag Document",
            ContentId = "floatDragDocument",
            Content = new Grid { Background = Brushes.White }
        });
        var manager = new DockingManager
        {
            Layout = new LayoutRoot
            {
                RootPanel = new LayoutPanel
                {
                    Orientation = Orientation.Horizontal,
                    Children = { toolPane, documentPane }
                }
            }
        };
        AutomationProperties.SetAutomationId(manager, "FloatDragManager");

        PreviewMouseDown += (s, e) => Log($"FLOAT PreviewDown pos={Fmt(e.GetPosition(this))} src={e.OriginalSource?.GetType().Name} captured={CapturedName()}");
        PreviewMouseMove += (s, e) =>
        {
            if (e.LeftButton == MouseButtonState.Pressed)
                Log($"FLOAT PreviewMove pos={Fmt(e.GetPosition(this))} src={e.OriginalSource?.GetType().Name} captured={CapturedName()}");
        };
        PreviewMouseUp += (s, e) => Log($"FLOAT PreviewUp pos={Fmt(e.GetPosition(this))} src={e.OriginalSource?.GetType().Name} captured={CapturedName()}");
        Content = manager;

        Loaded += (s, e) => Dispatcher.BeginInvoke(new Action(() =>
        {
            manager.UpdateLayout();
            var title = FindVisualDescendant<AnchorablePaneTitle>(manager, x => ReferenceEquals(x.Model, tool));
            if (title == null)
            {
                Log("FLOAT_RESULT: FAIL reason=title-not-found");
                return;
            }

            AutomationProperties.SetAutomationId(title, "FloatDragTitle");
            var topLeft = title.PointToScreen(new Point());
            Log($"FLOAT_MODE_READY x={topLeft.X:0.0} y={topLeft.Y:0.0} width={title.ActualWidth:0.0} height={title.ActualHeight:0.0}");

            System.Windows.Threading.DispatcherTimer? timer = null;
            title.PreviewMouseDown += (ts, te) =>
            {
                if (timer != null)
                    return;
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
                        var sourceReady = floating.IsVisible && source != null &&
                            (Math.Abs(floating.Left) > 0.1 || Math.Abs(floating.Top) > 0.1);
                        if (!sourceReady && ticks < 80)
                            return;
                        var coordinatesMatch = Math.Abs(screenOrigin.X - floating.Left) <= 2 &&
                            Math.Abs(screenOrigin.Y - floating.Top) <= 2;
                        timer.Stop();
                        Log($"FLOAT_COORDINATES window={floating.Left:0.0},{floating.Top:0.0} pointToScreen={screenOrigin.X:0.0},{screenOrigin.Y:0.0} compatibilityTargetScale={toDevice.M11:0.0},{toDevice.M22:0.0}");
                        Log($"FLOAT_RESULT: {(coordinatesMatch ? "PASS" : "FAIL")} floatingWindows={manager.FloatingWindows.Count()} isFloating={tool.IsFloating} coordinatesMatch={coordinatesMatch}");
                    }
                    else if (ticks >= 80)
                    {
                        timer.Stop();
                        Log($"FLOAT_RESULT: FAIL floatingWindows=0 isFloating={tool.IsFloating}");
                    }
                };
                timer.Start();
            };
        }));
    }

    // ---- Combo popup positioning probe (TOOLTIPTEST_COMBO_POPUP_MODE=1) --------------------------
    // Reported bug: OpenDevelop's toolbar ComboBox dropdown popup appears in the correct location,
    // but the class/member combo box above the code editor - which is nested inside AvalonDock's
    // docking chrome (DockingManager -> LayoutDocumentPane -> document content), rather than sitting
    // directly in the top-level window content like the toolbar - opens its popup far from the combo
    // box. This builds both shapes side by side in one window: ToolbarComboBox (already in XAML, at
    // top-level) and a second combo nested inside a DockingManager's document content (mimicking
    // QuickClassBrowser's own nesting depth), then opens each dropdown in turn and logs the popup's
    // actual screen position against the combo's own screen position, so the offset is directly
    // comparable between the two shapes.
    void ConfigureComboPopupMode()
    {
        Title = "ToolTipTest Combo Popup Position Probe";
        Width = 700;
        Height = 520;

        var documentComboBox = new ComboBox { Width = 160, SelectedIndex = 0 };
        documentComboBox.Items.Add(new ComboBoxItem { Content = "Obfuscar.Program" });
        documentComboBox.Items.Add(new ComboBoxItem { Content = "Obfuscar.Options" });
        documentComboBox.Items.Add(new ComboBoxItem { Content = "Obfuscar.Helper" });
        AutomationProperties.SetAutomationId(documentComboBox, "DocumentComboBox");
        documentComboBox.DropDownOpened += (s, e) => LogComboPopupPosition(documentComboBox, "Document");

        var browserBar = new Grid { Background = Brushes.WhiteSmoke, Height = 26 };
        browserBar.Children.Add(documentComboBox);

        var editorGrid = new Grid();
        editorGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        editorGrid.RowDefinitions.Add(new RowDefinition());
        Grid.SetRow(browserBar, 0);
        editorGrid.Children.Add(browserBar);
        var placeholder = new TextBlock { Text = "// code editor placeholder", Margin = new Thickness(8) };
        Grid.SetRow(placeholder, 1);
        editorGrid.Children.Add(placeholder);

        var documentPane = new LayoutDocumentPane();
        documentPane.Children.Add(new LayoutDocument
        {
            Title = "Program.cs",
            ContentId = "comboProbeDocument",
            Content = editorGrid
        });

        var manager = new DockingManager
        {
            Height = 320,
            Layout = new LayoutRoot { RootPanel = new LayoutPanel { Children = { documentPane } } }
        };
        AutomationProperties.SetAutomationId(manager, "ComboProbeManager");

        RootStackPanel.Children.Add(manager);

        AutomationProperties.SetAutomationId(ToolbarComboBox, "ToolbarComboBox");
        ToolbarComboBox.DropDownOpened += (s, e) => LogComboPopupPosition(ToolbarComboBox, "Toolbar");

        // Reported symptom is intermittent ("some randomness") on BOTH combos, not just the nested
        // one - so a single open/close of each proves little either way. Cycle each combo open/closed
        // many times (with the document combo only started once AvalonDock has actually realized it
        // into the live visual tree - a fixed delay isn't reliable) to catch the failure rate.
        //
        // Under TOOLTIPTEST_DEVFLOW=1, skip the auto-cycle-and-shutdown behavior entirely: open the
        // toolbar combo once and leave it open indefinitely, so an external DevFlow client can poll
        // /api/v1/ui/elements at its own pace instead of racing a fixed timer.
        var devFlowHoldOpenMode = Environment.GetEnvironmentVariable("TOOLTIPTEST_DEVFLOW") == "1";
        const int cyclesPerCombo = 15;
        Loaded += (s, e) =>
        {
            Log("COMBO_POPUP_MODE_READY");

            if (devFlowHoldOpenMode)
            {
                var target = Environment.GetEnvironmentVariable("TOOLTIPTEST_COMBO_TARGET");
                if (string.Equals(target, "document", StringComparison.OrdinalIgnoreCase))
                {
                    documentComboBox.IsDropDownOpen = true;
                    Log("COMBO_POPUP_MODE_HOLD_OPEN document dropdown opened and held; not auto-cycling");
                }
                else
                {
                    ToolbarComboBox.IsDropDownOpen = true;
                    Log("COMBO_POPUP_MODE_HOLD_OPEN toolbar dropdown opened and held; not auto-cycling");
                }
                return;
            }

            void RunCycles(ComboBox combo, string label, Action next)
            {
                var count = 0;
                var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
                var closing = false;
                timer.Tick += (ts, te) =>
                {
                    if (!closing)
                    {
                        closing = true;
                        combo.IsDropDownOpen = true;
                    }
                    else
                    {
                        closing = false;
                        combo.IsDropDownOpen = false;
                        count++;
                        if (count >= cyclesPerCombo)
                        {
                            timer.Stop();
                            next();
                        }
                    }
                };
                timer.Start();
            }

            void StartDocumentCycles()
            {
                if (!documentComboBox.IsLoaded || PresentationSource.FromVisual(documentComboBox) == null)
                {
                    Dispatcher.BeginInvoke(new Action(StartDocumentCycles), System.Windows.Threading.DispatcherPriority.Background);
                    return;
                }
                RunCycles(documentComboBox, "Document", () =>
                {
                    Log("COMBO_POPUP_MODE_DONE");
                    Application.Current.Shutdown();
                });
            }

            RunCycles(ToolbarComboBox, "Toolbar", StartDocumentCycles);
        };
    }

    void LogComboPopupPosition(ComboBox comboBox, string label)
    {
        if (PresentationSource.FromVisual(comboBox) == null)
        {
            Log($"{label} ComboBox DropDownOpened but combo box is not yet connected to a PresentationSource (skipping this cycle)");
            return;
        }
        Point comboScreen = comboBox.PointToScreen(new Point(0, 0));
        string popupInfo = "not-found";
        if (comboBox.Template?.FindName("PART_Popup", comboBox) is Popup popup && popup.Child is FrameworkElement popupChild
            && PresentationSource.FromVisual(popupChild) != null)
        {
            Point popupScreen = popupChild.PointToScreen(new Point(0, 0));
            double expectedX = comboScreen.X;
            double expectedY = comboScreen.Y + comboBox.ActualHeight;
            double offsetX = popupScreen.X - expectedX;
            double offsetY = popupScreen.Y - expectedY;
            var flag = (Math.Abs(offsetX) > 5 || Math.Abs(offsetY) > 5) ? " *** MISPOSITIONED ***" : "";
            popupInfo = $"popupScreen={popupScreen.X:0.0},{popupScreen.Y:0.0} expected={expectedX:0.0},{expectedY:0.0} offset={offsetX:0.0},{offsetY:0.0}{flag}";
        }
        Log($"{label} ComboBox DropDownOpened comboScreen={comboScreen.X:0.0},{comboScreen.Y:0.0} {popupInfo}");
    }

    static void DumpDescendantTypes(DependencyObject root, System.Collections.Generic.List<string> acc, int depth)
    {
        if (depth > 40) return;
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            acc.Add(child.GetType().Name);
            DumpDescendantTypes(child, acc, depth + 1);
        }
    }

    static T? FindVisualDescendant<T>(DependencyObject root, Func<T, bool> predicate) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match && predicate(match))
                return match;
            var nested = FindVisualDescendant(child, predicate);
            if (nested != null)
                return nested;
        }
        return null;
    }

    // ---- AvalonDock re-dock via drop target (TOOLTIPTEST_AVALONDOCK_DOCK_MODE=1) ----------------
    // The reverse of the tear-out float: a floating tool window is dragged over the main DockingManager,
    // whose OverlayWindow shows drop indicators; releasing on one re-docks the pane. On LibreWPF this
    // exercises: (a) the transient overlay/drop-target windows shown mid-drag (same family as the
    // splitter/float overlay bug), and (b) LayoutFloatingWindowControl's drag engine, which upstream is
    // driven by Win32 WM_MOVING/WM_EXITSIZEMOVE + an HwndSource hook - neither of which exists on the
    // portable PresentationSource, so this is expected to surface the porting gap until that drag path
    // is adapted. The harness starts with the tool already floating and drives a drop onto the document
    // pane center, then reports whether the pane re-docked (DOCK_RESULT: PASS/FAIL).
    LayoutDocumentPane? dockDocumentPane;
    LayoutAnchorable? dockTool;
    DockingManager? dockManager;
    Grid? dockDocumentContent;

    void ConfigureAvalonDockDockMode()
    {
        Title = "ToolTipTest AvalonDock Re-Dock Probe";
        Width = 900;
        Height = 620;
        Left = 100;
        Top = 100;
        WindowStartupLocation = WindowStartupLocation.Manual;

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
        dockDocumentPane.Children.Add(new LayoutDocument
        {
            Title = "Dock Document",
            ContentId = "dockDocument",
            Content = dockDocumentContent
        });
        dockManager = new DockingManager
        {
            Layout = new LayoutRoot
            {
                RootPanel = new LayoutPanel
                {
                    Orientation = Orientation.Horizontal,
                    Children = { toolPane, dockDocumentPane }
                }
            }
        };
        AutomationProperties.SetAutomationId(dockManager, "DockManager");

        PreviewMouseDown += (s, e) => Log($"DOCK PreviewDown pos={Fmt(e.GetPosition(this))} src={e.OriginalSource?.GetType().Name} captured={CapturedName()}");
        PreviewMouseMove += (s, e) =>
        {
            if (e.LeftButton == MouseButtonState.Pressed)
                Log($"DOCK PreviewMove pos={Fmt(e.GetPosition(this))} src={e.OriginalSource?.GetType().Name} captured={CapturedName()}");
        };
        PreviewMouseUp += (s, e) => Log($"DOCK PreviewUp pos={Fmt(e.GetPosition(this))} src={e.OriginalSource?.GetType().Name} captured={CapturedName()}");
        Content = dockManager;

        Loaded += (s, e) => Dispatcher.BeginInvoke(new Action(() =>
        {
            dockManager.UpdateLayout();

            // Start with the tool floating: this is the state whose re-dock we want to test. Float()
            // reparents the anchorable into a floating window; give the layout a beat to realize it.
            dockTool.Float();
            // Delay readiness so the per-window DPI scale has settled before we capture the drop-target
            // screen coordinates (they must match the scale DevFlow resolves at drag time).
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
            return;
        }

        dockManager.UpdateLayout();

        var floating = dockManager.FloatingWindows.FirstOrDefault();
        if (floating == null)
        {
            Log($"DOCK_RESULT: FAIL reason=no-floating-window isFloating={dockTool.IsFloating}");
            return;
        }

        // Float() may place the floating window under the macOS menu bar or behind the main window
        // (they fully overlap, so it's occluded and unclickable). Move it clear to the RIGHT of the
        // main window (which spans x 100..1000) so its caption is an unobstructed drag target, and
        // mark it topmost so it stays clickable.
        floating.Left = 1080;
        floating.Top = 220;
        floating.Topmost = true;
        floating.UpdateLayout();

        // The floating window's own title bar is the drag handle that moves the window (and drives the
        // drop-target engine). Fall back to the pane title inside it if the caption isn't found.
        // The floating window's caption is templated (Grid/Border + DropDownControlArea), not an
        // AnchorablePaneTitle. Upstream the caption drag is a Win32 gesture (AttachDrag sends
        // WM_NCLBUTTONDOWN/HT_CAPTION, then WM_MOVING drives DragService, WM_EXITSIZEMOVE drops) - none
        // of which exists on LibreWPF's portable window, so today no managed element starts the drag.
        // We target the caption's DropDownControlArea as the intended drag handle so this harness is
        // ready to pass once the floating-window drag engine is wired to portable window-move events.
        FrameworkElement? dragHandle =
            FindVisualDescendant<AnchorablePaneTitle>(floating, _ => true) as FrameworkElement
            ?? FindVisualDescendant<DropDownControlArea>(floating, _ => true);
        if (dragHandle == null)
        {
            var types = new System.Collections.Generic.List<string>();
            DumpDescendantTypes(floating, types, 0);
            Log("DOCK diag descendants: " + string.Join(", ", types.Distinct().Take(40)));
            Log("DOCK_RESULT: FAIL reason=drag-handle-not-found");
            return;
        }
        AutomationProperties.SetAutomationId(dragHandle, "DockDragTitle");

        // Drop point for the "dock inside" indicator: horizontally at the document pane center, but
        // vertically at the content-area center (above the pane's geometric center by half the tab
        // strip). Take X from the pane control and Y from the content element. The drag is driven as a
        // delta (dx/dy) from the handle - a delta is invariant to any absolute screen-scale offset, so
        // it lands correctly as long as handle and target share this PointToScreen space.
        var paneControl = FindVisualDescendant<LayoutDocumentPaneControl>(dockManager, _ => true) as FrameworkElement
            ?? dockManager;
        var content = (FrameworkElement?)dockDocumentContent ?? paneControl;
        var paneCenter = paneControl.PointToScreen(new Point(paneControl.ActualWidth / 2d, paneControl.ActualHeight / 2d));
        var contentCenter = content.PointToScreen(new Point(content.ActualWidth / 2d, content.ActualHeight / 2d));
        var docCenterScreen = new Point(paneCenter.X, contentCenter.Y);
        var handleScreen = dragHandle.PointToScreen(new Point(dragHandle.ActualWidth / 2d, dragHandle.ActualHeight / 2d));

        Log($"DOCK_MODE_READY handleX={handleScreen.X:0.0} handleY={handleScreen.Y:0.0} " +
            $"targetX={docCenterScreen.X:0.0} targetY={docCenterScreen.Y:0.0} " +
            $"floatingWindows={dockManager.FloatingWindows.Count()} isFloating={dockTool.IsFloating}");

        // Poll for re-dock: the tool leaves the floating state and rejoins a docked pane.
        var ticks = 0;
        var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        timer.Tick += (s, e) =>
        {
            ticks++;
            var docked = !dockTool.IsFloating && !dockManager.FloatingWindows.Any();
            if (docked)
            {
                timer.Stop();
                Log($"DOCK_RESULT: PASS floatingWindows={dockManager.FloatingWindows.Count()} isFloating={dockTool.IsFloating}");
            }
            else if (ticks >= 80)
            {
                timer.Stop();
                Log($"DOCK_RESULT: FAIL floatingWindows={dockManager.FloatingWindows.Count()} isFloating={dockTool.IsFloating}");
            }
        };
        timer.Start();
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

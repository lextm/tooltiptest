using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Automation;

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

    public MainWindow()
    {
        InitializeComponent();

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
        StatusText.Text = DateTime.Now.ToString("HH:mm:ss.fff") + " " + message;
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

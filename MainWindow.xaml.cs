using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace ToolTipTest;

public partial class MainWindow : Window
{
    Popup? rawPopup;

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
        };
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
}

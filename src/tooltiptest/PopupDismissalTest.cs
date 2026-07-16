using System;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace ToolTipTest;

class PopupDismissalTest : TestCase
{
    bool expectingSubmenuClose;
    bool submenuPrematureClose;
    bool nestedSubmenuOpened;
    bool nestedSubmenuPrematureClose;
    bool lazySubmenuRebuilt;
    MenuItem? recentWorkspacesMenuItem;
    bool comboPrematureClose;
    bool isVerifying;
    Popup? rawPopup;
    MenuItem? fileMenuItem, recentFilesMenuItem;
    ComboBox? toolbarComboBox;

    public PopupDismissalTest()
    {
        Name = "Popup Dismissal";
        Description = "Tests that popups/menus/dropdowns are not dismissed prematurely";
    }

    public override void Setup()
    {
        var dock = new DockPanel { Margin = new Thickness(16) };

        fileMenuItem = new MenuItem { Header = "_File" };
        fileMenuItem.SubmenuOpened += OnFileSubmenuOpened;
        fileMenuItem.SubmenuClosed += OnFileSubmenuClosed;

        var newMenuItem = new MenuItem { Header = "_New", InputGestureText = "Ctrl+N" };
        var projectItem = new MenuItem { Header = "_Project" };
        projectItem.Items.Add(new MenuItem { Header = "_WPF Application" });
        projectItem.Items.Add(new MenuItem { Header = "_Class Library" });
        newMenuItem.Items.Add(projectItem);
        var fileItem = new MenuItem { Header = "_File" };
        fileItem.Items.Add(new MenuItem { Header = "_XAML View" });
        fileItem.Items.Add(new MenuItem { Header = "_C# Class" });
        newMenuItem.Items.Add(fileItem);
        fileMenuItem.Items.Add(newMenuItem);
        fileMenuItem.Items.Add(new MenuItem { Header = "_Open...", InputGestureText = "Ctrl+O" });
        fileMenuItem.Items.Add(new Separator());

        recentFilesMenuItem = new MenuItem { Header = "Recent _Files" };
        AutomationProperties.SetAutomationId(recentFilesMenuItem, "RecentFilesMenuItem");
        recentFilesMenuItem.SubmenuOpened += OnRecentFilesSubmenuOpened;
        recentFilesMenuItem.SubmenuClosed += OnRecentFilesSubmenuClosed;
        recentFilesMenuItem.Items.Add(new MenuItem { Header = "dummy" });
        fileMenuItem.Items.Add(recentFilesMenuItem);
        fileMenuItem.Items.Add(new Separator());
        fileMenuItem.Items.Add(new MenuItem { Header = "E_xit", IsEnabled = false });

        var menu = new Menu { IsMainMenu = true };
        DockPanel.SetDock(menu, Dock.Top);
        menu.Items.Add(fileMenuItem);
        menu.Items.Add(new MenuItem { Header = "_Edit" });
        menu.Items.Add(new MenuItem { Header = "_Help" });

        var statusText = new TextBlock { Margin = new Thickness(0, 0, 0, 12), Text = "Running popup dismissal test...", TextWrapping = TextWrapping.Wrap };

        var plainButton = new Button { Content = "Plain Button" };
        ToolTipService.SetToolTip(plainButton, "Plain button tooltip text");
        ToolTipService.SetInitialShowDelay(plainButton, 0);
        var serviceButton = new Button { Content = "Service Button" };
        ToolTipService.SetToolTip(serviceButton, "Service button tooltip text");
        ToolTipService.SetInitialShowDelay(serviceButton, 0);
        toolbarComboBox = new ComboBox { Width = 140, SelectedIndex = 0 };
        ToolTipService.SetToolTip(toolbarComboBox, "Toolbar ComboBox dropdown");
        toolbarComboBox.Items.Add(new ComboBoxItem { Content = "System default" });
        toolbarComboBox.Items.Add(new ComboBoxItem { Content = "Light" });
        toolbarComboBox.Items.Add(new ComboBoxItem { Content = "Dark" });
        toolbarComboBox.Items.Add(new ComboBoxItem { Content = "High contrast" });
        toolbarComboBox.DropDownOpened += OnToolbarComboBoxDropDownOpened;
        toolbarComboBox.DropDownClosed += OnToolbarComboBoxDropDownClosed;

        var toolbar = new ToolBar();
        toolbar.Items.Add(plainButton);
        toolbar.Items.Add(new Separator());
        toolbar.Items.Add(serviceButton);
        toolbar.Items.Add(new Separator());
        toolbar.Items.Add(new TextBlock { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 6, 0), Text = "Theme:" });
        toolbar.Items.Add(toolbarComboBox);

        var stack = new StackPanel();
        stack.Children.Add(statusText);
        stack.Children.Add(toolbar);

        var forceShowBtn = new Button { Margin = new Thickness(0, 16, 0, 0), Content = "Force-show tooltip via code" };
        forceShowBtn.Click += OnForceShowClick;
        stack.Children.Add(forceShowBtn);

        var forcePopupBtn = new Button { Margin = new Thickness(0, 8, 0, 0), Content = "Force-show raw Popup" };
        forcePopupBtn.Click += OnForcePopupClick;
        stack.Children.Add(forcePopupBtn);

        var probeBtn = new Button { Margin = new Thickness(0, 8, 0, 0), Content = "Probe explicit ToolTip template" };
        stack.Children.Add(probeBtn);

        var probeToolTip = new ToolTip { Content = "Explicit template text" };
        var style = new Style(typeof(ToolTip));
        style.Setters.Add(new Setter(Control.ForegroundProperty, Brushes.Red));
        style.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.White));
        style.Setters.Add(new Setter(Control.BorderBrushProperty, Brushes.Blue));
        style.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(2)));
        style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(8)));
        var template = new ControlTemplate(typeof(ToolTip));
        var borderFactory = new FrameworkElementFactory(typeof(Border));
        borderFactory.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty));
        borderFactory.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Control.BorderBrushProperty));
        borderFactory.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Control.BorderThicknessProperty));
        borderFactory.SetValue(Border.PaddingProperty, new TemplateBindingExtension(Control.PaddingProperty));
        var textFactory = new FrameworkElementFactory(typeof(TextBlock));
        textFactory.SetValue(TextBlock.TextProperty, new TemplateBindingExtension(ContentControl.ContentProperty));
        textFactory.SetValue(TextBlock.ForegroundProperty, new TemplateBindingExtension(Control.ForegroundProperty));
        textFactory.SetValue(TextBlock.FontSizeProperty, 16.0);
        borderFactory.AppendChild(textFactory);
        template.VisualTree = borderFactory;
        style.Setters.Add(new Setter(Control.TemplateProperty, template));
        probeToolTip.Style = style;
        ToolTipService.SetToolTip(probeBtn, probeToolTip);
        ToolTipService.SetInitialShowDelay(probeBtn, 0);
        probeToolTip.Opened += (s, e) => DumpToolTipTree(probeToolTip, "explicit");

        plainButton.ToolTipOpening += (s, e) => Log("PlainButton.ToolTipOpening fired");
        plainButton.ToolTipClosing += (s, e) => Log("PlainButton.ToolTipClosing fired");
        serviceButton.ToolTipOpening += (s, e) => Log("ServiceButton.ToolTipOpening fired");
        plainButton.MouseEnter += (s, e) => Log("PlainButton.MouseEnter fired");
        plainButton.MouseLeave += (s, e) => Log("PlainButton.MouseLeave fired");

        dock.Children.Add(menu);
        dock.Children.Add(stack);
        ContentRoot = dock;
    }

    public override void Run()
    {
        CreateContentWindow("ToolTipTest Popup Dismissal", 520, 470);
        Log("MainWindow.Loaded fired");

        var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(2.5) };
        timer.Tick += (ts, te) => { timer.Stop(); Log("Auto-open timer fired; opening raw Popup"); OnForcePopupClick(null!, new RoutedEventArgs()); };
        timer.Start();

        var ttTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(4.5) };
        ttTimer.Tick += (ts, te) =>
        {
            ttTimer.Stop();
            Log("Auto-open timer fired; opening ServiceButton ToolTip");
            var svcBtn = FindChild<Button>(ContentRoot, b => b.Content as string == "Service Button");
            if (svcBtn != null && ToolTipService.GetToolTip(svcBtn) is ToolTip tt) { tt.PlacementTarget = svcBtn; tt.IsOpen = true; }
        };
        ttTimer.Start();

        var probeTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(7.5) };
        probeTimer.Tick += (ts, te) =>
        {
            probeTimer.Stop();
            if (rawPopup != null) rawPopup.IsOpen = false;
            var probe = FindChild<Button>(ContentRoot, b => b.Content as string == "Probe explicit ToolTip template");
            if (probe != null && ToolTipService.GetToolTip(probe) is ToolTip p) { p.PlacementTarget = probe; p.IsOpen = true; Log("Explicit probe IsOpen=" + p.IsOpen); }
        };
        probeTimer.Start();

        var menuTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
        menuTimer.Tick += (ts, te) => { menuTimer.Stop(); Log("Auto-open timer fired; opening main File menu Popup"); if (fileMenuItem != null) { fileMenuItem.Focus(); fileMenuItem.IsSubmenuOpen = true; } };
        menuTimer.Start();

        var recentFilesTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(11) };
        recentFilesTimer.Tick += (ts, te) => { recentFilesTimer.Stop(); Log("Auto-open timer fired; opening File > Recent Files submenu"); if (recentFilesMenuItem != null) recentFilesMenuItem.IsSubmenuOpen = true; };
        recentFilesTimer.Start();

        var recentWorkspacesTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(12) };
        recentWorkspacesTimer.Tick += (ts, te) =>
        {
            recentWorkspacesTimer.Stop();
            Log("Auto-open timer fired; opening File > Recent Files > Workspaces submenu");
            if (recentWorkspacesMenuItem != null) recentWorkspacesMenuItem.IsSubmenuOpen = true;
            else Log("Workspaces submenu not available after lazy rebuild");
        };
        recentWorkspacesTimer.Start();

        var comboTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(13) };
        comboTimer.Tick += (ts, te) =>
        {
            comboTimer.Stop();
            expectingSubmenuClose = true;
            if (recentWorkspacesMenuItem != null) recentWorkspacesMenuItem.IsSubmenuOpen = false;
            if (recentFilesMenuItem != null) recentFilesMenuItem.IsSubmenuOpen = false;
            if (fileMenuItem != null) fileMenuItem.IsSubmenuOpen = false;
            Log("Auto-open timer fired; opening toolbar ComboBox dropdown");
            if (toolbarComboBox != null) { toolbarComboBox.Focus(); toolbarComboBox.IsDropDownOpen = true; }
        };
        comboTimer.Start();

        var verifyTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(16) };
        verifyTimer.Tick += (ts, te) =>
        {
            verifyTimer.Stop();
            isVerifying = true;
            var pass = nestedSubmenuOpened && lazySubmenuRebuilt && !submenuPrematureClose && !nestedSubmenuPrematureClose && !comboPrematureClose;
            Log("RESULT: " + (pass ? "PASS" : "FAIL") + " nestedSubmenuOpened=" + nestedSubmenuOpened + " lazySubmenuRebuilt=" + lazySubmenuRebuilt + " submenuPrematureClose=" + submenuPrematureClose + " nestedSubmenuPrematureClose=" + nestedSubmenuPrematureClose + " comboPrematureClose=" + comboPrematureClose);
            SetResult(pass);
        };
        verifyTimer.Start();
    }

    static T? FindChild<T>(DependencyObject parent, Func<T, bool> predicate) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T match && predicate(match)) return match;
            var nested = FindChild(child, predicate);
            if (nested != null) return nested;
        }
        return null;
    }

    void OnForceShowClick(object sender, RoutedEventArgs e)
    {
        var plainButton = FindChild<Button>(ContentRoot, b => b.Content as string == "Plain Button");
        if (plainButton == null) return;
        var tooltip = ToolTipService.GetToolTip(plainButton);
        Log("GetToolTip returned: " + (tooltip?.GetType().Name ?? "null") + " value=" + tooltip);
        if (tooltip is ToolTip tt) { tt.PlacementTarget = plainButton; tt.IsOpen = false; tt.IsOpen = true; Log("Set ToolTip.IsOpen = true; IsOpen is now " + tt.IsOpen); }
    }

    void OnForcePopupClick(object sender, RoutedEventArgs e)
    {
        var button = FindChild<Button>(ContentRoot, b => b.Content as string == "Force-show raw Popup");
        if (button == null) return;
        var expectedTopLeft = button.PointToScreen(new Point(0, button.ActualHeight));
        Log($"Expected Bottom-placement top-left (via PointToScreen): {expectedTopLeft.X}, {expectedTopLeft.Y}");
        if (rawPopup != null) rawPopup.IsOpen = false;
        rawPopup = new Popup
        {
            PlacementTarget = button,
            Placement = PlacementMode.Bottom,
            StaysOpen = true,
            Child = new Border { Background = Brushes.Yellow, BorderBrush = Brushes.Black, BorderThickness = new Thickness(1), Padding = new Thickness(8), Child = new TextBlock { Text = "Raw Popup content" } }
        };
        rawPopup.IsOpen = true;
        Log("Raw Popup.IsOpen set to true; actual IsOpen=" + rawPopup.IsOpen);
    }

    void OnFileSubmenuOpened(object sender, RoutedEventArgs e)
    {
        if (fileMenuItem == null) return;
        var screenPoint = fileMenuItem.PointToScreen(new Point(0, 0));
        Log("Main File menu SubmenuOpened fired; IsSubmenuOpen=" + fileMenuItem.IsSubmenuOpen + " targetScreen=" + screenPoint.X + "," + screenPoint.Y);
    }

    void OnFileSubmenuClosed(object sender, RoutedEventArgs e)
    {
        if (!expectingSubmenuClose && !isVerifying) { submenuPrematureClose = true; Log("Main File menu SubmenuClosed fired UNEXPECTEDLY"); }
        else Log("Main File menu SubmenuClosed fired (expected)");
    }

    void OnRecentFilesSubmenuOpened(object sender, RoutedEventArgs e)
    {
        if (recentFilesMenuItem == null || !ReferenceEquals(e.OriginalSource, recentFilesMenuItem)) return;
        nestedSubmenuOpened = true;
        RebuildRecentFilesSubmenu();
        var screenPoint = recentFilesMenuItem.PointToScreen(new Point(0, 0));
        Log("File > Recent Files SubmenuOpened fired; IsSubmenuOpen=" + recentFilesMenuItem.IsSubmenuOpen + " targetScreen=" + screenPoint.X + "," + screenPoint.Y);
    }

    void RebuildRecentFilesSubmenu()
    {
        if (recentFilesMenuItem == null) return;
        recentFilesMenuItem.Items.Clear();
        recentFilesMenuItem.Items.Add(new MenuItem { Header = "Sample.xaml" });
        recentFilesMenuItem.Items.Add(new MenuItem { Header = "PopupNotes.txt" });
        recentFilesMenuItem.Items.Add(new Separator());
        recentWorkspacesMenuItem = new MenuItem { Header = "_Workspaces" };
        AutomationProperties.SetAutomationId(recentWorkspacesMenuItem, "RecentWorkspacesMenuItem");
        recentWorkspacesMenuItem.SubmenuOpened += (s, e) => { nestedSubmenuOpened = true; Log("Workspaces SubmenuOpened fired"); };
        recentWorkspacesMenuItem.SubmenuClosed += (s, e) => { if (!expectingSubmenuClose && !isVerifying) { nestedSubmenuPrematureClose = true; Log("Workspaces SubmenuClosed UNEXPECTEDLY"); } else Log("Workspaces SubmenuClosed (expected)"); };
        recentWorkspacesMenuItem.Items.Add(new MenuItem { Header = "LibreWPF Debugging" });
        recentWorkspacesMenuItem.Items.Add(new MenuItem { Header = "OpenDevelop Layout" });
        recentFilesMenuItem.Items.Add(recentWorkspacesMenuItem);
        lazySubmenuRebuilt = true;
        Log("File > Recent Files lazy submenu rebuilt; childCount=" + recentFilesMenuItem.Items.Count);
    }

    void OnRecentFilesSubmenuClosed(object sender, RoutedEventArgs e)
    {
        if (!expectingSubmenuClose && !isVerifying) { nestedSubmenuPrematureClose = true; Log("File > Recent Files SubmenuClosed UNEXPECTEDLY"); }
        else Log("File > Recent Files SubmenuClosed fired (expected)");
    }

    void OnToolbarComboBoxDropDownOpened(object? sender, EventArgs e)
    {
        if (toolbarComboBox == null) return;
        var screenPoint = toolbarComboBox.PointToScreen(new Point(0, 0));
        Log("Toolbar ComboBox DropDownOpened fired; IsDropDownOpen=" + toolbarComboBox.IsDropDownOpen + " targetScreen=" + screenPoint.X + "," + screenPoint.Y);
    }

    void OnToolbarComboBoxDropDownClosed(object? sender, EventArgs e)
    {
        if (!isVerifying) { comboPrematureClose = true; Log("Toolbar ComboBox DropDownClosed UNEXPECTEDLY"); }
        else Log("Toolbar ComboBox DropDownClosed fired (shutdown teardown)");
    }

    void DumpToolTipTree(ToolTip toolTip, string name)
    {
        toolTip.ApplyTemplate();
        Log(name + ": IsOpen=" + toolTip.IsOpen + ", Content=" + toolTip.Content + ", Actual=" + toolTip.ActualWidth + "x" + toolTip.ActualHeight + ", Foreground=" + toolTip.Foreground + ", Background=" + toolTip.Background + ", Template=" + (toolTip.Template == null ? "null" : toolTip.Template.GetType().Name));
        DumpVisualTree(toolTip, name, 0);
    }

    void DumpVisualTree(DependencyObject node, string name, int depth)
    {
        if (depth > 8) return;
        Log(name + ": tree depth=" + depth + " type=" + node.GetType().FullName + (node is FrameworkElement fe ? " actual=" + fe.ActualWidth + "x" + fe.ActualHeight + " desired=" + fe.DesiredSize : ""));
        int count;
        try { count = VisualTreeHelper.GetChildrenCount(node); } catch (Exception ex) { Log(name + ": tree error=" + ex.GetType().Name + ": " + ex.Message); return; }
        for (var i = 0; i < count; i++) DumpVisualTree(VisualTreeHelper.GetChild(node, i), name, depth + 1);
    }
}

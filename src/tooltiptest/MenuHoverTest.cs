using System;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ToolTipTest;

class MenuHoverTest : TestCase
{
    bool nestedSubmenuOpened, nestedSubmenuPrematureClose, lazySubmenuRebuilt, submenuPrematureClose, isVerifying;
    MenuItem? recentWorkspacesMenuItem;

    public MenuHoverTest()
    {
        Name = "Menu Hover";
        Description = "Tests nested submenus stay open during hover interaction";
    }

    public override void Setup()
    {
        var fileMenuItem = new MenuItem { Header = "_File" };
        fileMenuItem.SubmenuClosed += (s, e) => { if (!isVerifying) { submenuPrematureClose = true; Log("Main File menu SubmenuClosed fired UNEXPECTEDLY"); } };

        var recentFilesMenuItem = new MenuItem { Header = "Recent _Files" };
        AutomationProperties.SetAutomationId(recentFilesMenuItem, "RecentFilesMenuItem");
        recentFilesMenuItem.SubmenuOpened += (s, e) =>
        {
            if (!ReferenceEquals(e.OriginalSource, recentFilesMenuItem)) return;
            nestedSubmenuOpened = true;
            recentFilesMenuItem.Items.Clear();
            recentFilesMenuItem.Items.Add(new MenuItem { Header = "Sample.xaml" });
            recentFilesMenuItem.Items.Add(new MenuItem { Header = "PopupNotes.txt" });
            recentFilesMenuItem.Items.Add(new Separator());
            recentWorkspacesMenuItem = new MenuItem { Header = "_Workspaces" };
            AutomationProperties.SetAutomationId(recentWorkspacesMenuItem, "RecentWorkspacesMenuItem");
            recentWorkspacesMenuItem.SubmenuOpened += (s2, e2) => { nestedSubmenuOpened = true; Log("Workspaces SubmenuOpened fired"); };
            recentWorkspacesMenuItem.SubmenuClosed += (s2, e2) => { if (!isVerifying) { nestedSubmenuPrematureClose = true; Log("Workspaces SubmenuClosed UNEXPECTEDLY"); } };
            recentWorkspacesMenuItem.Items.Add(new MenuItem { Header = "LibreWPF Debugging" });
            recentWorkspacesMenuItem.Items.Add(new MenuItem { Header = "OpenDevelop Layout" });
            recentFilesMenuItem.Items.Add(recentWorkspacesMenuItem);
            lazySubmenuRebuilt = true;
            Log("File > Recent Files lazy submenu rebuilt");
        };
        recentFilesMenuItem.SubmenuClosed += (s, e) => { if (!isVerifying) { nestedSubmenuPrematureClose = true; Log("File > Recent Files SubmenuClosed UNEXPECTEDLY"); } };
        recentFilesMenuItem.Items.Add(new MenuItem { Header = "dummy" });
        fileMenuItem.Items.Add(recentFilesMenuItem);

        var menu = new Menu { IsMainMenu = true };
        menu.Items.Add(fileMenuItem);
        menu.Items.Add(new MenuItem { Header = "_Edit" });
        menu.Items.Add(new MenuItem { Header = "_Help" });
        ContentRoot = menu;
    }

    public override void Run()
    {
        CreateContentWindow("ToolTipTest Menu Hover", 520, 470);
        var hoverReadyTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        hoverReadyTimer.Tick += (ts, te) =>
        {
            hoverReadyTimer.Stop();
            var fileMenuItem = FindVisualDescendant<MenuItem>(ContentRoot, m => m.Header as string == "_File");
            if (fileMenuItem != null)
            {
                fileMenuItem.Focus();
                fileMenuItem.IsSubmenuOpen = true;
                Log("HOVER_STEP File menu opened by test setup");
                StartHoverModeVerificationTimer();
            }
        };
        hoverReadyTimer.Start();
    }

    void StartHoverModeVerificationTimer()
    {
        var verifyTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(18) };
        verifyTimer.Tick += (ts, te) =>
        {
            verifyTimer.Stop();
            isVerifying = true;
            var pass = nestedSubmenuOpened && lazySubmenuRebuilt && !submenuPrematureClose && !nestedSubmenuPrematureClose;
            Log("RESULT: " + (pass ? "PASS" : "FAIL") + " mode=hover nestedSubmenuOpened=" + nestedSubmenuOpened + " lazySubmenuRebuilt=" + lazySubmenuRebuilt + " submenuPrematureClose=" + submenuPrematureClose + " nestedSubmenuPrematureClose=" + nestedSubmenuPrematureClose);
            SetResult(pass);
        };
        verifyTimer.Start();
    }
}

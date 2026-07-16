using System;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using AvalonDock;
using AvalonDock.Layout;

namespace ToolTipTest;

class ComboPopupTest : TestCase
{
    public ComboPopupTest()
    {
        Name = "Combo Popup Position";
        Description = "Tests ComboBox dropdown popup positioning in toolbar and inside AvalonDock";
    }

    public override void Setup()
    {
        var rootStack = new StackPanel { Margin = new Thickness(16) };

        var toolbarComboBox = new ComboBox { Width = 140, SelectedIndex = 0 };
        AutomationProperties.SetAutomationId(toolbarComboBox, "ToolbarComboBox");
        toolbarComboBox.Items.Add(new ComboBoxItem { Content = "System default" });
        toolbarComboBox.Items.Add(new ComboBoxItem { Content = "Light" });
        toolbarComboBox.Items.Add(new ComboBoxItem { Content = "Dark" });
        toolbarComboBox.Items.Add(new ComboBoxItem { Content = "High contrast" });
        toolbarComboBox.DropDownOpened += (s, e) => LogComboPopupPosition(toolbarComboBox, "Toolbar");

        var toolbar = new ToolBar();
        toolbar.Items.Add(new TextBlock { VerticalAlignment = VerticalAlignment.Center, Text = "Toolbar Combo:" });
        toolbar.Items.Add(toolbarComboBox);
        rootStack.Children.Add(toolbar);

        var documentComboBox = new ComboBox { Width = 160, SelectedIndex = 0 };
        AutomationProperties.SetAutomationId(documentComboBox, "DocumentComboBox");
        documentComboBox.Items.Add(new ComboBoxItem { Content = "Obfuscar.Program" });
        documentComboBox.Items.Add(new ComboBoxItem { Content = "Obfuscar.Options" });
        documentComboBox.Items.Add(new ComboBoxItem { Content = "Obfuscar.Helper" });
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
        documentPane.Children.Add(new LayoutDocument { Title = "Program.cs", ContentId = "comboProbeDocument", Content = editorGrid });

        var manager = new DockingManager { Height = 320, Layout = new LayoutRoot { RootPanel = new LayoutPanel { Children = { documentPane } } } };
        AutomationProperties.SetAutomationId(manager, "ComboProbeManager");
        rootStack.Children.Add(manager);

        ContentRoot = rootStack;
    }

    public override void Run()
    {
        CreateContentWindow("ToolTipTest Combo Popup Position Probe", 700, 520);
        const int cyclesPerCombo = 15;
        Log("COMBO_POPUP_MODE_READY");

        var toolbarCombo = FindComboBox("ToolbarComboBox");
        var documentCombo = FindComboBox("DocumentComboBox");
        if (toolbarCombo == null || documentCombo == null) { SetResult(false); return; }

        void RunCycles(ComboBox combo, string label, Action next)
        {
            var count = 0;
            var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
            var closing = false;
            timer.Tick += (ts, te) =>
            {
                if (!closing) { closing = true; combo.IsDropDownOpen = true; }
                else
                {
                    closing = false; combo.IsDropDownOpen = false; count++;
                    if (count >= cyclesPerCombo) { timer.Stop(); next(); }
                }
            };
            timer.Start();
        }

        void StartDocumentCycles()
        {
            if (!documentCombo.IsLoaded || PresentationSource.FromVisual(documentCombo) == null)
            {
                System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(new Action(StartDocumentCycles), System.Windows.Threading.DispatcherPriority.Background);
                return;
            }
            RunCycles(documentCombo, "Document", () =>
            {
                Log("COMBO_POPUP_MODE_DONE");
                SetResult(true);
            });
        }

        RunCycles(toolbarCombo, "Toolbar", StartDocumentCycles);
    }

    ComboBox? FindComboBox(string automationId)
    {
        return FindVisualDescendant<ComboBox>(ContentRoot, c =>
        {
            string? id = null;
            try { id = AutomationProperties.GetAutomationId(c); } catch { }
            return id == automationId;
        });
    }

    void LogComboPopupPosition(ComboBox comboBox, string label)
    {
        if (PresentationSource.FromVisual(comboBox) == null)
        {
            Log(label + " ComboBox DropDownOpened but combo box is not yet connected to a PresentationSource (skipping this cycle)");
            return;
        }
        var comboScreen = comboBox.PointToScreen(new Point(0, 0));
        var popupInfo = "not-found";
        if (comboBox.Template?.FindName("PART_Popup", comboBox) is Popup popup && popup.Child is FrameworkElement popupChild && PresentationSource.FromVisual(popupChild) != null)
        {
            var popupScreen = popupChild.PointToScreen(new Point(0, 0));
            var expectedX = comboScreen.X;
            var expectedY = comboScreen.Y + comboBox.ActualHeight;
            var offsetX = popupScreen.X - expectedX;
            var offsetY = popupScreen.Y - expectedY;
            var flag = (Math.Abs(offsetX) > 5 || Math.Abs(offsetY) > 5) ? " *** MISPOSITIONED ***" : "";
            popupInfo = $"popupScreen={popupScreen.X:0.0},{popupScreen.Y:0.0} expected={expectedX:0.0},{expectedY:0.0} offset={offsetX:0.0},{offsetY:0.0}{flag}";
        }
        Log(label + " ComboBox DropDownOpened comboScreen=" + comboScreen.X + "," + comboScreen.Y + " " + popupInfo);
    }
}

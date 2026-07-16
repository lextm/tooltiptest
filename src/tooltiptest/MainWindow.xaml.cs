using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ToolTipTest;

public partial class MainWindow : Window
{
    readonly TestRunner runner = new();
    readonly Dictionary<TestCase, StackPanel> testRows = new();

    public MainWindow()
    {
        InitializeComponent();
        runner.LogMessage += OnRunnerLog;
        runner.TestStarted += OnTestStarted;
        runner.TestCompleted += OnTestCompleted;

        RegisterTests();
        BuildTestList();
        runner.StartCli();
    }

    void RegisterTests()
    {
        runner.Register(new PopupDismissalTest());
        runner.Register(new ImageRenderingTest());
        runner.Register(new SplitterCaptureTest());
        runner.Register(new MenuHoverTest());
        runner.Register(new ComboPopupTest());
        runner.Register(new AvalonDockFloatTest());
        runner.Register(new AvalonDockDockTest());
    }

    void BuildTestList()
    {
        TestListPanel.Children.Clear();
        testRows.Clear();

        foreach (var test in runner.Tests)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 2) };

            var checkBox = new CheckBox { IsChecked = test.IsEnabled, VerticalAlignment = VerticalAlignment.Center, Width = 20 };
            checkBox.Checked += (s, e) => test.IsEnabled = true;
            checkBox.Unchecked += (s, e) => test.IsEnabled = false;

            var nameBlock = new TextBlock { Text = test.Name, FontWeight = FontWeights.Bold, Width = 180, VerticalAlignment = VerticalAlignment.Center };
            var descBlock = new TextBlock { Text = test.Description, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center, Foreground = Brushes.Gray, Margin = new Thickness(8, 0, 0, 0) };
            var statusBlock = new TextBlock { Text = "Not Run", Width = 80, TextAlignment = TextAlignment.Right, VerticalAlignment = VerticalAlignment.Center, Foreground = Brushes.Gray };

            row.Children.Add(checkBox);
            row.Children.Add(nameBlock);
            row.Children.Add(descBlock);
            row.Children.Add(statusBlock);

            TestListPanel.Children.Add(row);
            testRows[test] = row;
        }

        var runLabel = new TextBlock
        {
            Text = "Type 'run' in console to start, or 'enable <name>' / 'disable <name>' to toggle tests.",
            Margin = new Thickness(0, 12, 0, 0), Foreground = Brushes.Gray, FontStyle = FontStyles.Italic
        };
        TestListPanel.Children.Add(runLabel);
    }

    void UpdateTestStatus(TestCase test)
    {
        if (!testRows.TryGetValue(test, out var row)) return;
        var statusBlock = (TextBlock)row.Children[3];
        if (test.IsRunning) { statusBlock.Text = "Running..."; statusBlock.Foreground = Brushes.Orange; }
        else if (test.Result == true) { statusBlock.Text = "PASS"; statusBlock.Foreground = Brushes.Green; }
        else if (test.Result == false) { statusBlock.Text = "FAIL"; statusBlock.Foreground = Brushes.Red; }
        else { statusBlock.Text = "Not Run"; statusBlock.Foreground = Brushes.Gray; }
    }

    void OnTestStarted(TestCase test)
    {
        Dispatcher.Invoke(() => UpdateTestStatus(test));
    }

    void OnTestCompleted(TestCase test, bool? result)
    {
        Dispatcher.Invoke(() => UpdateTestStatus(test));
    }

    void OnRunnerLog(string message)
    {
        Dispatcher.Invoke(() =>
        {
            ConsoleOutput.Text += message + "\n";
            ConsoleScroll.ScrollToBottom();
        });
    }

    void OnRunAllClick(object sender, RoutedEventArgs e)
    {
        _ = runner.RunAllAsync();
    }

    public void AutoRun()
    {
        var t = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        t.Tick += (s, e) => { t.Stop(); _ = runner.RunAllAsync(); };
        t.Start();
    }

    void OnRunSelectedClick(object sender, RoutedEventArgs e)
    {
        _ = runner.RunAllAsync();
    }

    void OnToggleAllClick(object sender, RoutedEventArgs e)
    {
        var allEnabled = runner.Tests.All(t => t.IsEnabled);
        foreach (var test in runner.Tests)
            test.IsEnabled = !allEnabled;
        BuildTestList();
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        Environment.Exit(0);
    }
}

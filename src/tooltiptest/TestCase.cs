using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace ToolTipTest;

abstract class TestCase
{
    public string Name { get; protected set; } = "";
    public string Description { get; protected set; } = "";
    public bool IsEnabled { get; set; } = true;
    public bool? Result { get; set; }
    public bool IsRunning { get; set; }

    protected Window? TestWindow { get; set; }
    protected DependencyObject ContentRoot { get; set; } = null!;

    public event Action<TestCase, bool?>? Completed;
    public event Action<string>? LogMessage;

    public abstract void Setup();
    public abstract void Run();
    public virtual void Cleanup() { }

    protected void CreateContentWindow(string title, int width = 800, int height = 600)
    {
        TestWindow = new Window
        {
            Title = title,
            Width = width,
            Height = height,
            WindowStartupLocation = WindowStartupLocation.Manual
        };
        if (ContentRoot is UIElement ui)
            TestWindow.Content = ui;
        TestWindow.Show();
    }

    protected void SetResult(bool passed)
    {
        Result = passed;
        IsRunning = false;
        Completed?.Invoke(this, passed);
        if (TestWindow != null)
        {
            try { TestWindow.Close(); } catch { }
            TestWindow = null;
        }
    }

    protected void Log(string message)
    {
        var rendered = DateTime.Now.ToString("HH:mm:ss.fff") + " [tooltiptest] " + message;
        Console.WriteLine(rendered);
        LogMessage?.Invoke(rendered);
        try
        {
            System.IO.File.AppendAllText("/tmp/tooltiptest_debug.log",
                DateTime.Now.ToString("HH:mm:ss.fff") + " [app] " + message + "\n");
        }
        catch { }
    }

    protected static string CapturedName() => Mouse.Captured?.GetType().Name ?? "null";
    protected static string Fmt(Point p) => $"{p.X:0},{p.Y:0}";

    protected static T? FindVisualDescendant<T>(DependencyObject root, Func<T, bool> predicate) where T : DependencyObject
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

    protected static void DumpDescendantTypes(DependencyObject node, List<string> acc, int depth)
    {
        if (depth > 40) return;
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++)
        {
            var child = VisualTreeHelper.GetChild(node, i);
            acc.Add(child.GetType().Name);
            DumpDescendantTypes(child, acc, depth + 1);
        }
    }
}

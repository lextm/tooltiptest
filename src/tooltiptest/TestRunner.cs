using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace ToolTipTest;

class TestRunner
{
    readonly List<TestCase> tests = new();
    TestCase? current;
    CancellationTokenSource? cliCts;

    public IReadOnlyList<TestCase> Tests => tests;
    public TestCase? Current => current;

    public event Action<TestCase>? TestStarted;
    public event Action<TestCase, bool?>? TestCompleted;
    public event Action<string>? LogMessage;

    public void Register(TestCase test)
    {
        test.Completed += (t, r) =>
        {
            TestCompleted?.Invoke(t, r);
            current = null;
        };
        test.LogMessage += msg => LogMessage?.Invoke(msg);
        tests.Add(test);
    }

    public async Task RunAllAsync()
    {
        foreach (var test in tests.Where(t => t.IsEnabled))
        {
            await RunSingleAsync(test);
        }
        Log("All tests completed.");
        await Application.Current.Dispatcher.InvokeAsync(() => Application.Current.Shutdown());
    }

    public async Task RunSingleAsync(TestCase test)
    {
        if (current != null) return;
        current = test;
        test.Result = null;
        test.IsRunning = true;

        TestStarted?.Invoke(test);
        Log($"Starting test: {test.Name}");

        var tcs = new TaskCompletionSource<bool?>();
        void handler(TestCase t, bool? r) => tcs.TrySetResult(r);
        test.Completed += handler;
        var timeout = Task.Delay(30000);

        await Application.Current.Dispatcher.InvokeAsync(() => test.Setup());
        await Task.Delay(500);
        await Application.Current.Dispatcher.InvokeAsync(() => test.Run());

        var completed = await Task.WhenAny(tcs.Task, timeout);
        test.Completed -= handler;

        if (completed == timeout)
        {
            test.Result = false;
            test.IsRunning = false;
            Log($"TIMEOUT: {test.Name} did not complete within 120s");
        }

        await Application.Current.Dispatcher.InvokeAsync(() => test.Cleanup());
        TestCompleted?.Invoke(test, test.Result);
        current = null;
    }

    public void StartCli()
    {
        cliCts = new CancellationTokenSource();
        Task.Run(() => CliLoop(cliCts.Token));
    }

    void CliLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var line = Console.ReadLine();
            if (line == null) break;
            ProcessCommand(line.Trim());
        }
    }

    void ProcessCommand(string line)
    {
        if (string.IsNullOrWhiteSpace(line)) return;
        var parts = line.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);

        switch (parts[0].ToLower())
        {
            case "enable":
                if (parts.Length > 1)
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        foreach (var t in tests)
                            if (t.Name.IndexOf(parts[1], StringComparison.OrdinalIgnoreCase) >= 0)
                                t.IsEnabled = true;
                    });
                break;
            case "disable":
                if (parts.Length > 1)
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        foreach (var t in tests)
                            if (t.Name.IndexOf(parts[1], StringComparison.OrdinalIgnoreCase) >= 0)
                                t.IsEnabled = false;
                    });
                break;
            case "run":
                if (parts.Length > 1)
                {
                    var match = tests.FirstOrDefault(t =>
                        t.Name.IndexOf(parts[1], StringComparison.OrdinalIgnoreCase) >= 0);
                    if (match != null)
                        _ = RunSingleAsync(match);
                }
                else
                    _ = RunAllAsync();
                break;
            case "status":
                foreach (var t in tests)
                    Log($"{t.Name}: {(t.IsEnabled ? "enabled" : "disabled")} result={t.Result?.ToString() ?? "not-run"}");
                break;
            case "quit":
                Application.Current.Dispatcher.Invoke(() => Application.Current.Shutdown());
                break;
            default:
                Log($"Unknown command: {line}");
                break;
        }
    }

    void Log(string msg)
    {
        var rendered = DateTime.Now.ToString("HH:mm:ss.fff") + " [runner] " + msg;
        Console.WriteLine(rendered);
        LogMessage?.Invoke(rendered);
    }
}

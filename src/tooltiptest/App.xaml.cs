using System;
using System.Windows;

namespace ToolTipTest;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        if (Environment.GetEnvironmentVariable("TOOLTIPTEST_DEVFLOW") == "1")
        {
            var agentType = Type.GetType("LeXtudio.DevFlow.Agent.Wpf.WpfAgentServiceExtensions, LeXtudio.DevFlow.Agent.LibreWpf");
            if (agentType != null)
            {
                var method = agentType.GetMethod("AddWpfDevFlowAgent",
                    new Type[] { typeof(Application), typeof(object) });
                method?.Invoke(null, new object[] { this, new { Port = 9523 } });
            }
        }

        var window = new MainWindow();
        window.Show();
        window.AutoRun();
    }
}

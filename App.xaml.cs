using System.Windows;
using LeXtudio.DevFlow.Agent.WPF;
using Microsoft.Maui.DevFlow.Agent.Core;

namespace ToolTipTest;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        if (System.Environment.GetEnvironmentVariable("TOOLTIPTEST_DEVFLOW") == "1")
            this.AddWpfDevFlowAgent(new AgentOptions { Port = 9523 });
    }
}

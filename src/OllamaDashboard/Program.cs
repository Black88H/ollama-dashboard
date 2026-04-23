using Velopack;

namespace OllamaDashboard;

/// <summary>
/// Custom WPF entry point — required by Velopack.
/// VelopackApp.Build().Run() MUST be the very first call before any WPF/DI code.
/// On a normal launch this is a no-op (returns immediately).
/// When the app relaunches after a downloaded update it applies the update and
/// exits before the WPF app even initialises.
/// </summary>
public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        VelopackApp.Build().Run();

        var app = new App();
        app.InitializeComponent();
        app.Run();
    }
}

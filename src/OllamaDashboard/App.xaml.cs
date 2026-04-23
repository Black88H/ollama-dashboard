using System.IO;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OllamaDashboard.Services;
using OllamaDashboard.ViewModels;
using OllamaDashboard.Views;
using Serilog;

namespace OllamaDashboard;

public partial class App : Application
{
    public static IHost? Host { get; private set; }
    public static IServiceProvider Services => Host!.Services;

    protected override async void OnStartup(StartupEventArgs e)
    {
        // Global safety net: catches any unhandled exception during startup and
        // shows it in a MessageBox before the process exits silently.
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            var msg = args.ExceptionObject is Exception ex ? ex.ToString() : args.ExceptionObject?.ToString();
            MessageBox.Show(msg, "Kritischer Startfehler", MessageBoxButton.OK, MessageBoxImage.Error);
        };
        DispatcherUnhandledException += (_, args) =>
        {
            MessageBox.Show(args.Exception.ToString(), "UI-Fehler beim Start",
                MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };

        try
        {

        // Logging first, so anything that throws during DI is captured.
        var logDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "OllamaDashboard", "logs");
        Directory.CreateDirectory(logDir);

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(
                Path.Combine(logDir, "app-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14)
            .CreateLogger();

        Host = Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder()
            .ConfigureServices((_, services) =>
            {
                // Services (singletons so settings + HTTP clients are shared)
                services.AddSingleton<ISettingsService, SettingsService>();
                services.AddSingleton<IThemeService, ThemeService>();
                services.AddSingleton<IPdfService, PdfService>();
                services.AddSingleton<IModelRegistry, ModelRegistry>();
                services.AddSingleton<IScriptAnalysisService, ScriptAnalysisService>();

                services.AddHttpClient<IOllamaService, OllamaService>();
                // UpdateService uses Velopack internally (no HttpClient needed here)
                services.AddSingleton<IUpdateService, UpdateService>();

                // ViewModels
                services.AddSingleton<MainViewModel>();
                services.AddSingleton<ChatViewModel>();
                services.AddSingleton<ScriptExtractorViewModel>();
                services.AddSingleton<SettingsViewModel>();

                // Views
                services.AddSingleton<MainWindow>();
            })
            .ConfigureLogging(lb =>
            {
                lb.ClearProviders();
                lb.AddSerilog(Log.Logger, dispose: true);
            })
            .Build();

        await Host.StartAsync();

        // Load persisted settings before any VM touches them.
        var settings = Services.GetRequiredService<ISettingsService>();
        await settings.LoadAsync();

        // Apply the saved theme before the window is shown.
        var themeService = Services.GetRequiredService<IThemeService>();
        themeService.Apply(settings.Current.Theme);

        // Fetch installed Ollama models in the background so the dropdown is pre-filled.
        var registry = Services.GetRequiredService<IModelRegistry>();
        _ = Task.Run(() => registry.RefreshAsync());

        var mainWindow = Services.GetRequiredService<MainWindow>();
        mainWindow.DataContext = Services.GetRequiredService<MainViewModel>();
        mainWindow.Show();

        // Optional: startup update check (fire-and-forget)
        if (settings.Current.CheckUpdatesOnStartup)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    var updater = Services.GetRequiredService<IUpdateService>();
                    var result = await updater.CheckForUpdateAsync();
                    if (result.UpdateAvailable)
                    {
                        // The Settings view will display it when the user navigates there.
                        var settingsVm = Services.GetRequiredService<SettingsViewModel>();
                        Dispatcher.Invoke(() =>
                            settingsVm.LastUpdateResult = result);
                    }
                }
                catch (Exception ex)
                {
                    Log.Logger.Warning(ex, "Background update check failed");
                }
            });
        }

        base.OnStartup(e);

        } // end try
        catch (Exception ex)
        {
            MessageBox.Show(ex.ToString(), "Startfehler — Details",
                MessageBoxButton.OK, MessageBoxImage.Error);
            Log.CloseAndFlush();
            Shutdown(1);
        }
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (Host is not null)
        {
            await Host.StopAsync(TimeSpan.FromSeconds(5));
            Host.Dispose();
        }
        Log.CloseAndFlush();
        base.OnExit(e);
    }
}

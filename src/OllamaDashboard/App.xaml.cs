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
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            var msg = args.ExceptionObject is Exception ex ? ex.ToString() : args.ExceptionObject?.ToString();
            MessageBox.Show(msg, "Kritischer Startfehler", MessageBoxButton.OK, MessageBoxImage.Error);
        };
        DispatcherUnhandledException += (_, args) =>
        {
            MessageBox.Show(args.Exception.ToString(), "UI-Fehler",
                MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };

        try
        {
            // ── Logging ───────────────────────────────────────────────────────
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

            // ── SQLCipher battery init (must come before any EF Core call) ───
            SQLitePCL.Batteries_V2.Init();

            // ── DI container ──────────────────────────────────────────────────
            Host = Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder()
                .ConfigureServices((_, services) =>
                {
                    // Core services
                    services.AddSingleton<ISettingsService,      SettingsService>();
                    services.AddSingleton<IThemeService,         ThemeService>();
                    services.AddSingleton<IPdfService,           PdfService>();
                    services.AddSingleton<IModelRegistry,        ModelRegistry>();
                    services.AddSingleton<IScriptAnalysisService, ScriptAnalysisService>();
                    services.AddSingleton<IUpdateService,        UpdateService>();

                    // StudyCoach-specific services
                    services.AddSingleton<IAuthService,          AuthService>();
                    services.AddSingleton<IDatabaseService,      DatabaseService>();
                    services.AddSingleton<ILicenseService,       LicenseService>();
                    services.AddSingleton<IUserProgressService,  UserProgressService>();

                    // HTTP services
                    services.AddHttpClient<IOllamaService, OllamaService>();
                    services.AddHttpClient<IGroqService,   GroqService>();

                    // ViewModels
                    services.AddSingleton<LoginViewModel>();
                    services.AddSingleton<MainViewModel>();
                    services.AddSingleton<DashboardViewModel>();
                    services.AddSingleton<ChatViewModel>();
                    services.AddSingleton<ScriptExtractorViewModel>();
                    services.AddSingleton<SettingsViewModel>();

                    // Views
                    services.AddSingleton<LoginWindow>();
                    services.AddSingleton<MainWindow>();
                })
                .ConfigureLogging(lb =>
                {
                    lb.ClearProviders();
                    lb.AddSerilog(Log.Logger, dispose: true);
                })
                .Build();

            await Host.StartAsync();

            // ── Settings ──────────────────────────────────────────────────────
            var settings = Services.GetRequiredService<ISettingsService>();
            await settings.LoadAsync();

            var themeService = Services.GetRequiredService<IThemeService>();
            themeService.Apply(settings.Current.Theme);

            // ── Authentication gate ───────────────────────────────────────────
            var authService = Services.GetRequiredService<IAuthService>();
            var loginVm     = Services.GetRequiredService<LoginViewModel>();
            var loginWindow = Services.GetRequiredService<LoginWindow>();
            loginWindow.DataContext = loginVm;

            string? dbKey = null;

            // Subscribe to success event to capture the derived DB key
            loginVm.AuthSucceeded += (_, key) => dbKey = key;

            var authResult = loginWindow.ShowDialog();
            if (authResult != true || dbKey is null)
            {
                // User closed the window without authenticating
                Shutdown();
                return;
            }

            // ── Database init ─────────────────────────────────────────────────
            var dbService = Services.GetRequiredService<IDatabaseService>();
            await dbService.InitializeAsync(dbKey);

            var user = await dbService.GetOrCreateUserAsync();

            // Sync license tier from DB
            if (Services.GetRequiredService<ILicenseService>() is LicenseService ls)
                ls.SyncFromUser();

            // ── Daily XP / Streak ─────────────────────────────────────────────
            var progress = Services.GetRequiredService<IUserProgressService>();
            await progress.OnDailyLoginAsync();

            // ── Ollama model discovery (background) ───────────────────────────
            var registry = Services.GetRequiredService<IModelRegistry>();
            _ = Task.Run(() => registry.RefreshAsync());

            // ── Show main window ──────────────────────────────────────────────
            var mainWindow = Services.GetRequiredService<MainWindow>();
            mainWindow.DataContext = Services.GetRequiredService<MainViewModel>();
            mainWindow.Show();

            // Kick off dashboard refresh now that we have data
            _ = Services.GetRequiredService<DashboardViewModel>().RefreshAsync();

            // ── Startup update check (fire-and-forget) ────────────────────────
            if (settings.Current.CheckUpdatesOnStartup)
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var updater   = Services.GetRequiredService<IUpdateService>();
                        var result    = await updater.CheckForUpdateAsync();
                        if (result.UpdateAvailable)
                        {
                            var settingsVm = Services.GetRequiredService<SettingsViewModel>();
                            Dispatcher.Invoke(() => settingsVm.LastUpdateResult = result);
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Logger.Warning(ex, "Background update check failed");
                    }
                });
            }

            base.OnStartup(e);
        }
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

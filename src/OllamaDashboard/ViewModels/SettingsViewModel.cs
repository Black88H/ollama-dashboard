using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using OllamaDashboard.Models;
using OllamaDashboard.Services;

namespace OllamaDashboard.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly ISettingsService _settings;
    private readonly IOllamaService _ollama;
    private readonly IUpdateService _updater;
    private readonly IModelRegistry _modelRegistry;
    private readonly ILogger<SettingsViewModel> _logger;

    /// <summary>Live list of installed Ollama models (owned by the registry).</summary>
    public ReadOnlyObservableCollection<OllamaModelInfo> AvailableModels => _modelRegistry.AvailableModels;

    [ObservableProperty]
    private string _ollamaBaseUrl = string.Empty;

    [ObservableProperty]
    private string _selectedModel = string.Empty;

    [ObservableProperty]
    private double _temperature;

    [ObservableProperty]
    private int _contextWindow;

    [ObservableProperty]
    private string _gitHubOwner = string.Empty;

    [ObservableProperty]
    private string _gitHubRepo = string.Empty;

    [ObservableProperty]
    private bool _checkUpdatesOnStartup;

    [ObservableProperty]
    private bool _includePrereleases;

    [ObservableProperty]
    private string _connectionStatus = "Unbekannt";

    [ObservableProperty]
    private bool _isCheckingConnection;

    // Update
    [ObservableProperty]
    private string _currentVersion = "0.0.0";

    [ObservableProperty]
    private string? _updateStatus;

    [ObservableProperty]
    private UpdateCheckResult? _lastUpdateResult;

    [ObservableProperty]
    private bool _isCheckingUpdates;

    [ObservableProperty]
    private bool _isDownloadingUpdate;

    [ObservableProperty]
    private double _downloadProgress;

    [ObservableProperty]
    private string _downloadStage = string.Empty;

    public SettingsViewModel(
        ISettingsService settings,
        IOllamaService ollama,
        IUpdateService updater,
        IModelRegistry modelRegistry,
        ILogger<SettingsViewModel> logger)
    {
        _settings = settings;
        _ollama = ollama;
        _updater = updater;
        _modelRegistry = modelRegistry;
        _logger = logger;

        LoadFromSettings();
        CurrentVersion = _updater.CurrentVersion;

        // Keep SelectedModel in sync with the registry-driven global active model.
        _modelRegistry.ActiveModelChanged += (_, _) =>
            SelectedModel = _settings.Current.SelectedModel;
    }

    private void LoadFromSettings()
    {
        OllamaBaseUrl = _settings.Current.OllamaBaseUrl;
        SelectedModel = _settings.Current.SelectedModel;
        Temperature = _settings.Current.Temperature;
        ContextWindow = _settings.Current.ContextWindow;
        GitHubOwner = _settings.Current.GitHubOwner;
        GitHubRepo = _settings.Current.GitHubRepo;
        CheckUpdatesOnStartup = _settings.Current.CheckUpdatesOnStartup;
        IncludePrereleases = _settings.Current.IncludePrereleases;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        _settings.Current.OllamaBaseUrl         = OllamaBaseUrl;
        _settings.Current.SelectedModel         = SelectedModel;
        _settings.Current.Temperature           = Temperature;
        _settings.Current.ContextWindow         = ContextWindow;
        _settings.Current.GitHubOwner           = GitHubOwner;
        _settings.Current.GitHubRepo            = GitHubRepo;
        _settings.Current.CheckUpdatesOnStartup = CheckUpdatesOnStartup;
        _settings.Current.IncludePrereleases    = IncludePrereleases;

        await _settings.SaveAsync();
        MessageBox.Show("Einstellungen gespeichert.", "Erfolgreich",
            MessageBoxButton.OK, MessageBoxImage.Information);
    }

    [RelayCommand]
    private async Task TestConnectionAsync()
    {
        IsCheckingConnection = true;
        ConnectionStatus = "Prüfe…";
        try
        {
            // Persist the base URL first so the service uses the current UI value.
            _settings.Current.OllamaBaseUrl = OllamaBaseUrl;
            await _settings.SaveAsync();

            await _modelRegistry.RefreshAsync();
            ConnectionStatus = _modelRegistry.IsLoaded
                ? $"✓ Verbunden — {_modelRegistry.StatusMessage}"
                : $"✗ {_modelRegistry.StatusMessage}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Connection test failed");
            ConnectionStatus = $"✗ {ex.Message}";
        }
        finally
        {
            IsCheckingConnection = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanCheckUpdates))]
    private async Task CheckUpdatesAsync()
    {
        IsCheckingUpdates = true;
        UpdateStatus = "Suche nach Updates…";
        try
        {
            _settings.Current.GitHubOwner        = GitHubOwner;
            _settings.Current.GitHubRepo         = GitHubRepo;
            _settings.Current.IncludePrereleases = IncludePrereleases;
            await _settings.SaveAsync();

            LastUpdateResult = await _updater.CheckForUpdateAsync();

            UpdateStatus = LastUpdateResult switch
            {
                { HasError: true } r => $"Fehler: {r.ErrorMessage}",
                { UpdateAvailable: true } r =>
                    $"Neue Version verfügbar: v{r.LatestVersion} (aktuell: v{r.CurrentVersion})",
                { UpdateAvailable: false } =>
                    $"Du bist aktuell auf der neuesten Version (v{CurrentVersion}).",
                _ => "Unbekannter Zustand."
            };
        }
        finally
        {
            IsCheckingUpdates = false;
            InstallUpdateCommand.NotifyCanExecuteChanged();
        }
    }

    private bool CanCheckUpdates() => !IsCheckingUpdates && !IsDownloadingUpdate;

    [RelayCommand(CanExecute = nameof(CanInstallUpdate))]
    private async Task InstallUpdateAsync()
    {
        if (LastUpdateResult is null || !LastUpdateResult.UpdateAvailable) return;

        var confirm = MessageBox.Show(
            $"Version {LastUpdateResult.LatestVersion} herunterladen und installieren?\n\n" +
            "Die App wird anschließend automatisch neu gestartet.",
            "Update bestätigen",
            MessageBoxButton.OKCancel,
            MessageBoxImage.Question);
        if (confirm != MessageBoxResult.OK) return;

        IsDownloadingUpdate = true;
        DownloadProgress = 0;
        DownloadStage = "Starte Download…";
        var progress = new Progress<UpdateProgress>(p =>
        {
            DownloadProgress = p.PercentComplete;
            DownloadStage = p.StageDescription;
        });

        try
        {
            var staged = await _updater.DownloadAndStageUpdateAsync(LastUpdateResult, progress);
            UpdateStatus = "Update bereit. Starte neu…";
            _updater.ApplyUpdateAndRestart(staged);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Update failed");
            UpdateStatus = $"Update fehlgeschlagen: {ex.Message}";
            MessageBox.Show(ex.Message, "Fehler beim Update",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsDownloadingUpdate = false;
        }
    }

    private bool CanInstallUpdate() =>
        LastUpdateResult?.UpdateAvailable == true && !IsDownloadingUpdate;

    partial void OnLastUpdateResultChanged(UpdateCheckResult? value) =>
        InstallUpdateCommand.NotifyCanExecuteChanged();
    partial void OnIsCheckingUpdatesChanged(bool value) =>
        CheckUpdatesCommand.NotifyCanExecuteChanged();
    partial void OnIsDownloadingUpdateChanged(bool value)
    {
        CheckUpdatesCommand.NotifyCanExecuteChanged();
        InstallUpdateCommand.NotifyCanExecuteChanged();
    }

    // Model changes in Settings flow back through the registry so sidebar + other VMs update.
    partial void OnSelectedModelChanged(string value)
    {
        if (!string.IsNullOrWhiteSpace(value) && value != _settings.Current.SelectedModel)
        {
            _ = _modelRegistry.SetActiveModelAsync(value);
        }
    }
}

using System.Collections.ObjectModel;
using System.Windows;
using Microsoft.Extensions.Logging;
using OllamaDashboard.Models;

namespace OllamaDashboard.Services;

public sealed class ModelRegistry : IModelRegistry
{
    private readonly IOllamaService _ollama;
    private readonly ISettingsService _settings;
    private readonly ILogger<ModelRegistry> _logger;
    private readonly ObservableCollection<OllamaModelInfo> _models = new();

    public ReadOnlyObservableCollection<OllamaModelInfo> AvailableModels { get; }
    public string ActiveModel => _settings.Current.SelectedModel;
    public bool IsLoaded { get; private set; }
    public string StatusMessage { get; private set; } = "Noch nicht geladen";

    public event EventHandler? ModelsRefreshed;
    public event EventHandler? ActiveModelChanged;

    public ModelRegistry(
        IOllamaService ollama,
        ISettingsService settings,
        ILogger<ModelRegistry> logger)
    {
        _ollama = ollama;
        _settings = settings;
        _logger = logger;
        AvailableModels = new ReadOnlyObservableCollection<OllamaModelInfo>(_models);
    }

    public async Task RefreshAsync(CancellationToken ct = default)
    {
        try
        {
            var available = await _ollama.IsAvailableAsync(ct);
            if (!available)
            {
                RunOnUiThread(() => _models.Clear());
                StatusMessage = "Ollama nicht erreichbar. Läuft der Dienst?";
                IsLoaded = false;
                ModelsRefreshed?.Invoke(this, EventArgs.Empty);
                return;
            }

            var list = await _ollama.GetInstalledModelsAsync(ct);

            // Refresh the ObservableCollection on the UI thread so WPF bindings update safely.
            RunOnUiThread(() =>
            {
                _models.Clear();
                foreach (var m in list.OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase))
                    _models.Add(m);
            });

            StatusMessage = _models.Count == 0
                ? "Keine Modelle installiert. Führe z. B. `ollama pull llama3.1:8b` aus."
                : $"{_models.Count} Modelle verfügbar";

            // Self-heal: if configured model is not installed, pick the first available.
            if (_models.Count > 0 &&
                !_models.Any(m => m.Name.Equals(_settings.Current.SelectedModel, StringComparison.OrdinalIgnoreCase)))
            {
                var fallback = _models[0].Name;
                _logger.LogInformation(
                    "Configured model '{Configured}' not installed. Falling back to '{Fallback}'.",
                    _settings.Current.SelectedModel, fallback);
                await SetActiveModelAsync(fallback, ct);
            }

            IsLoaded = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Model registry refresh failed");
            StatusMessage = $"Fehler: {ex.Message}";
            IsLoaded = false;
        }
        finally
        {
            ModelsRefreshed?.Invoke(this, EventArgs.Empty);
        }
    }

    public async Task SetActiveModelAsync(string modelName, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(modelName)) return;
        if (string.Equals(_settings.Current.SelectedModel, modelName, StringComparison.Ordinal))
            return;

        _settings.Current.SelectedModel = modelName;
        await _settings.SaveAsync(ct);
        _logger.LogInformation("Active model changed to {Model}", modelName);
        ActiveModelChanged?.Invoke(this, EventArgs.Empty);
    }

    private static void RunOnUiThread(Action action)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
            action();
        else
            dispatcher.Invoke(action);
    }
}

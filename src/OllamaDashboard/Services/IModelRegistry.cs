using System.Collections.ObjectModel;
using OllamaDashboard.Models;

namespace OllamaDashboard.Services;

public interface IModelRegistry
{
    /// <summary>
    /// All models currently installed in Ollama. Observable — bind directly from XAML.
    /// Updated by RefreshAsync().
    /// </summary>
    ReadOnlyObservableCollection<OllamaModelInfo> AvailableModels { get; }

    /// <summary>The currently active model name used for chat + extraction.</summary>
    string ActiveModel { get; }

    /// <summary>True once at least one successful refresh has completed.</summary>
    bool IsLoaded { get; }

    /// <summary>Last status message — useful for surfacing errors in UI.</summary>
    string StatusMessage { get; }

    /// <summary>
    /// Re-queries Ollama for the installed models list. Updates AvailableModels.
    /// Also self-heals ActiveModel: if the configured model isn't installed,
    /// silently falls back to the first available.
    /// </summary>
    Task RefreshAsync(CancellationToken ct = default);

    /// <summary>Changes the active model and persists the choice.</summary>
    Task SetActiveModelAsync(string modelName, CancellationToken ct = default);

    /// <summary>Fired after RefreshAsync completes (success or failure).</summary>
    event EventHandler? ModelsRefreshed;

    /// <summary>Fired whenever ActiveModel changes.</summary>
    event EventHandler? ActiveModelChanged;
}

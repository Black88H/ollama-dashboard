using OllamaDashboard.Models;

namespace OllamaDashboard.Services;

public interface ISettingsService
{
    /// <summary>Currently loaded settings. Modify properties and call SaveAsync to persist.</summary>
    AppSettings Current { get; }

    /// <summary>Loads settings from disk. Call once during app startup.</summary>
    Task LoadAsync(CancellationToken ct = default);

    /// <summary>Persists Current to disk.</summary>
    Task SaveAsync(CancellationToken ct = default);

    /// <summary>Fired whenever settings are saved.</summary>
    event EventHandler? SettingsChanged;
}

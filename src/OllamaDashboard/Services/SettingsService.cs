using System.IO;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using OllamaDashboard.Models;

namespace OllamaDashboard.Services;

public sealed class SettingsService : ISettingsService
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly ILogger<SettingsService> _logger;
    private readonly string _settingsPath;

    public AppSettings Current { get; private set; } = new();

    public event EventHandler? SettingsChanged;

    public SettingsService(ILogger<SettingsService> logger)
    {
        _logger = logger;

        var appDataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "OllamaDashboard");
        Directory.CreateDirectory(appDataDir);
        _settingsPath = Path.Combine(appDataDir, "settings.json");
    }

    public async Task LoadAsync(CancellationToken ct = default)
    {
        try
        {
            if (!File.Exists(_settingsPath))
            {
                _logger.LogInformation("No settings file found, using defaults");
                await SaveAsync(ct);
                return;
            }

            await using var fs = File.OpenRead(_settingsPath);
            var loaded = await JsonSerializer.DeserializeAsync<AppSettings>(fs, JsonOpts, ct);
            if (loaded is not null)
            {
                Current = loaded;
                _logger.LogInformation("Settings loaded from {Path}", _settingsPath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load settings; reverting to defaults");
            Current = new AppSettings();
        }
    }

    public async Task SaveAsync(CancellationToken ct = default)
    {
        try
        {
            await using var fs = File.Create(_settingsPath);
            await JsonSerializer.SerializeAsync(fs, Current, JsonOpts, ct);
            SettingsChanged?.Invoke(this, EventArgs.Empty);
            _logger.LogDebug("Settings saved to {Path}", _settingsPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save settings");
            throw;
        }
    }
}

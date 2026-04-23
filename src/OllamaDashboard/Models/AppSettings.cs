namespace OllamaDashboard.Models;

/// <summary>
/// User-editable settings persisted to %AppData%\OllamaDashboard\settings.json.
/// Class is mutable on purpose — ISettingsService performs the save.
/// </summary>
public sealed class AppSettings
{
    public string OllamaBaseUrl { get; set; } = "http://localhost:11434";
    public string SelectedModel { get; set; } = "llama3:latest";
    public double Temperature { get; set; } = 0.7;
    public int ContextWindow { get; set; } = 4096;
    public AppTheme Theme { get; set; } = AppTheme.System;

    // Update
    public string GitHubOwner { get; set; } = "Black88H";
    public string GitHubRepo { get; set; } = "ollama-dashboard";
    public bool CheckUpdatesOnStartup { get; set; } = true;
    public bool IncludePrereleases { get; set; } = false;
    public DateTime? LastUpdateCheck { get; set; }

    // Script extractor defaults
    public SummaryDetailLevel DefaultSummaryDetail { get; set; } = SummaryDetailLevel.Medium;

    // Groq API (free server-side AI, optional alternative to local Ollama)
    public string GroqApiKey { get; set; } = string.Empty;
    public string GroqModel { get; set; } = "llama-3.3-70b-versatile";
    public bool UseGroqForExtraction { get; set; } = true; // Groq ist Standard — fällt auf Ollama zurück wenn kein Key konfiguriert
}

public enum AppTheme
{
    Light,
    Dark,
    System
}

public enum SummaryDetailLevel
{
    VeryShort = 1, // Only the core concepts, ~5% of original
    Short     = 2, // Key exam topics, ~10%
    Medium    = 3, // Balanced summary, ~20%
    Detailed  = 4, // Full detail with examples, ~35%
    VeryDetailed = 5 // Near-complete, ~50%
}

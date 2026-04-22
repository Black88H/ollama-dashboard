using System.Text.Json.Serialization;

namespace OllamaDashboard.Models;

/// <summary>Result of an update check returned by IUpdateService.</summary>
public sealed class UpdateCheckResult
{
    public bool UpdateAvailable { get; init; }
    public string? CurrentVersion { get; init; }
    public string? LatestVersion { get; init; }
    public string? ReleaseNotes { get; init; }
    public string? DownloadUrl { get; init; }
    public long? DownloadSizeBytes { get; init; }
    public DateTime? PublishedAt { get; init; }
    public string? ErrorMessage { get; init; }
    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);
}

/// <summary>Progress reported during the download phase of an update.</summary>
public sealed class UpdateProgress
{
    public long BytesDownloaded { get; init; }
    public long? TotalBytes { get; init; }
    public double PercentComplete =>
        TotalBytes is > 0 ? (double)BytesDownloaded / TotalBytes.Value * 100 : 0;
    public string StageDescription { get; init; } = string.Empty;
}

// --- GitHub API DTOs ------------------------------------------------------
// https://docs.github.com/en/rest/releases/releases#get-the-latest-release

public sealed class GitHubRelease
{
    [JsonPropertyName("tag_name")]
    public string TagName { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("body")]
    public string? Body { get; set; }

    [JsonPropertyName("prerelease")]
    public bool Prerelease { get; set; }

    [JsonPropertyName("draft")]
    public bool Draft { get; set; }

    [JsonPropertyName("published_at")]
    public DateTime? PublishedAt { get; set; }

    [JsonPropertyName("assets")]
    public List<GitHubAsset> Assets { get; set; } = new();
}

public sealed class GitHubAsset
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("browser_download_url")]
    public string BrowserDownloadUrl { get; set; } = string.Empty;

    [JsonPropertyName("size")]
    public long Size { get; set; }

    [JsonPropertyName("content_type")]
    public string? ContentType { get; set; }
}

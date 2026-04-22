using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using OllamaDashboard.Models;
using Semver;

namespace OllamaDashboard.Services;

public sealed class UpdateService : IUpdateService
{
    private const string UserAgent = "OllamaDashboard-Updater";

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _http;
    private readonly ISettingsService _settings;
    private readonly ILogger<UpdateService> _logger;

    public string CurrentVersion { get; }

    public UpdateService(HttpClient http, ISettingsService settings, ILogger<UpdateService> logger)
    {
        _http = http;
        _settings = settings;
        _logger = logger;

        // GitHub requires a User-Agent header or it returns 403.
        if (!_http.DefaultRequestHeaders.UserAgent.Any())
        {
            _http.DefaultRequestHeaders.UserAgent.Add(
                new ProductInfoHeaderValue(UserAgent, GetAssemblyVersion()));
        }
        _http.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));

        CurrentVersion = GetAssemblyVersion();
    }

    private static string GetAssemblyVersion()
    {
        var v = Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0);
        return $"{v.Major}.{v.Minor}.{v.Build}";
    }

    public async Task<UpdateCheckResult> CheckForUpdateAsync(CancellationToken ct = default)
    {
        var owner = _settings.Current.GitHubOwner;
        var repo = _settings.Current.GitHubRepo;

        if (string.IsNullOrWhiteSpace(owner) || string.IsNullOrWhiteSpace(repo))
        {
            return new UpdateCheckResult
            {
                CurrentVersion = CurrentVersion,
                ErrorMessage = "GitHub-Repository ist nicht konfiguriert."
            };
        }

        var url = _settings.Current.IncludePrereleases
            ? $"https://api.github.com/repos/{owner}/{repo}/releases"
            : $"https://api.github.com/repos/{owner}/{repo}/releases/latest";

        try
        {
            GitHubRelease? release;
            if (_settings.Current.IncludePrereleases)
            {
                var releases = await _http.GetFromJsonAsync<List<GitHubRelease>>(url, JsonOpts, ct);
                release = releases?.FirstOrDefault(r => !r.Draft);
            }
            else
            {
                release = await _http.GetFromJsonAsync<GitHubRelease>(url, JsonOpts, ct);
            }

            if (release is null)
            {
                return new UpdateCheckResult
                {
                    CurrentVersion = CurrentVersion,
                    ErrorMessage = "Kein Release gefunden."
                };
            }

            _settings.Current.LastUpdateCheck = DateTime.UtcNow;
            await _settings.SaveAsync(ct);

            var tag = (release.TagName ?? string.Empty).TrimStart('v', 'V');
            if (!SemVersion.TryParse(tag, SemVersionStyles.Any, out var remote) ||
                !SemVersion.TryParse(CurrentVersion, SemVersionStyles.Any, out var local))
            {
                return new UpdateCheckResult
                {
                    CurrentVersion = CurrentVersion,
                    LatestVersion = release.TagName,
                    ErrorMessage = "Versions-Tag konnte nicht geparst werden (erwartet: semver wie '1.2.3')."
                };
            }

            var hasUpdate = remote.ComparePrecedenceTo(local) > 0;

            // Prefer the first .zip asset.
            var asset = release.Assets.FirstOrDefault(
                a => a.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase));

            return new UpdateCheckResult
            {
                UpdateAvailable = hasUpdate,
                CurrentVersion = CurrentVersion,
                LatestVersion = tag,
                ReleaseNotes = release.Body,
                DownloadUrl = asset?.BrowserDownloadUrl,
                DownloadSizeBytes = asset?.Size,
                PublishedAt = release.PublishedAt,
                ErrorMessage = hasUpdate && asset is null
                    ? "Release enthält keinen ZIP-Anhang."
                    : null
            };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "GitHub update check failed");
            return new UpdateCheckResult
            {
                CurrentVersion = CurrentVersion,
                ErrorMessage = $"Netzwerkfehler: {ex.Message}"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during update check");
            return new UpdateCheckResult
            {
                CurrentVersion = CurrentVersion,
                ErrorMessage = ex.Message
            };
        }
    }

    public async Task<string> DownloadAndStageUpdateAsync(
        UpdateCheckResult update,
        IProgress<UpdateProgress>? progress = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(update.DownloadUrl))
            throw new InvalidOperationException("Kein Download-Link vorhanden.");

        var stagingRoot = Path.Combine(Path.GetTempPath(), "OllamaDashboardUpdate");
        if (Directory.Exists(stagingRoot)) Directory.Delete(stagingRoot, true);
        Directory.CreateDirectory(stagingRoot);

        var zipPath = Path.Combine(stagingRoot, "update.zip");
        var extractedDir = Path.Combine(stagingRoot, "extracted");

        progress?.Report(new UpdateProgress { StageDescription = "Lade Update…" });

        // Streamed download with progress
        using (var resp = await _http.GetAsync(
                   update.DownloadUrl,
                   HttpCompletionOption.ResponseHeadersRead,
                   ct))
        {
            resp.EnsureSuccessStatusCode();
            var total = resp.Content.Headers.ContentLength ?? update.DownloadSizeBytes;

            await using var netStream = await resp.Content.ReadAsStreamAsync(ct);
            await using var fileStream = File.Create(zipPath);
            var buffer = new byte[81920];
            long totalRead = 0;
            int read;
            while ((read = await netStream.ReadAsync(buffer, ct)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, read), ct);
                totalRead += read;
                progress?.Report(new UpdateProgress
                {
                    BytesDownloaded = totalRead,
                    TotalBytes = total,
                    StageDescription = "Lade Update…"
                });
            }
        }

        progress?.Report(new UpdateProgress { StageDescription = "Entpacke Update…" });
        Directory.CreateDirectory(extractedDir);
        ZipFile.ExtractToDirectory(zipPath, extractedDir, overwriteFiles: true);

        File.Delete(zipPath);

        _logger.LogInformation("Update staged at {Path}", extractedDir);
        return extractedDir;
    }

    public void ApplyUpdateAndRestart(string stagedUpdateFolder)
    {
        var installDir = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
        var exePath = Process.GetCurrentProcess().MainModule?.FileName
                      ?? Path.Combine(installDir, "OllamaDashboard.exe");
        var updaterPath = Path.Combine(installDir, "OllamaDashboard.Updater.exe");

        if (!File.Exists(updaterPath))
            throw new FileNotFoundException("Updater.exe nicht gefunden", updaterPath);

        var pid = Environment.ProcessId;
        var args =
            $"--pid {pid} " +
            $"--source \"{stagedUpdateFolder}\" " +
            $"--target \"{installDir}\" " +
            $"--relaunch \"{exePath}\"";

        Process.Start(new ProcessStartInfo
        {
            FileName = updaterPath,
            Arguments = args,
            UseShellExecute = true,
            CreateNoWindow = true,
            WorkingDirectory = Path.GetTempPath()
        });

        // Give the updater a moment to spawn, then shut down.
        System.Windows.Application.Current.Dispatcher.BeginInvoke(() =>
            System.Windows.Application.Current.Shutdown());
    }
}

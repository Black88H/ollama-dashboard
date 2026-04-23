using System.Reflection;
using Microsoft.Extensions.Logging;
using OllamaDashboard.Models;
using Velopack;
using Velopack.Sources;
using VpkUpdateInfo = Velopack.UpdateInfo; // avoids clash with Models namespace

namespace OllamaDashboard.Services;

/// <summary>
/// Velopack-backed implementation of IUpdateService.
/// Uses GithubSource so the app locates update packages directly in GitHub Releases.
/// The public interface is identical to the old implementation — SettingsViewModel
/// requires zero changes.
/// </summary>
public sealed class UpdateService : IUpdateService
{
    private readonly ISettingsService _settings;
    private readonly ILogger<UpdateService> _logger;

    // Cached after CheckForUpdatesAsync so DownloadAndStage + Apply can reuse it.
    // Velopack's apply step needs the exact same UpdateInfo object from the check.
    private VpkUpdateInfo? _pendingUpdate;

    public string CurrentVersion { get; } = GetAssemblyVersion();

    public UpdateService(ISettingsService settings, ILogger<UpdateService> logger)
    {
        _settings = settings;
        _logger   = logger;
    }

    // -------------------------------------------------------------------------
    // IUpdateService
    // -------------------------------------------------------------------------

    public async Task<UpdateCheckResult> CheckForUpdateAsync(CancellationToken ct = default)
    {
        var mgr = CreateManager();
        if (mgr is null)
        {
            return Error("GitHub-Repository nicht konfiguriert. " +
                         "Bitte Owner und Repository in den Einstellungen eintragen.");
        }

        // Not installed = running from IDE / debug output folder.
        if (!mgr.IsInstalled)
        {
            _logger.LogInformation("Velopack: app not installed (dev mode) — update check skipped");
            return new UpdateCheckResult
            {
                UpdateAvailable = false,
                CurrentVersion  = CurrentVersion,
                LatestVersion   = CurrentVersion
            };
        }

        try
        {
            var info = await mgr.CheckForUpdatesAsync();

            _settings.Current.LastUpdateCheck = DateTime.UtcNow;
            await _settings.SaveAsync(ct);

            if (info is null)
            {
                return new UpdateCheckResult
                {
                    UpdateAvailable = false,
                    CurrentVersion  = CurrentVersion,
                    LatestVersion   = CurrentVersion
                };
            }

            _pendingUpdate = info; // cache for download step
            return new UpdateCheckResult
            {
                UpdateAvailable = true,
                CurrentVersion  = CurrentVersion,
                LatestVersion   = info.TargetFullRelease.Version.ToString(),
                ReleaseNotes    = info.TargetFullRelease.NotesMarkdown
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Update check failed");
            return Error(ex.Message);
        }
    }

    public async Task<string> DownloadAndStageUpdateAsync(
        UpdateCheckResult _result,
        IProgress<UpdateProgress>? progress = null,
        CancellationToken ct = default)
    {
        var mgr = CreateManager()
            ?? throw new InvalidOperationException("GitHub-Repository nicht konfiguriert.");

        if (!mgr.IsInstalled)
            throw new InvalidOperationException(
                "Velopack-Installer erforderlich — App läuft im Entwicklungsmodus.");

        // Re-fetch if cache is empty (defensive path when user skips explicit check).
        if (_pendingUpdate is null)
        {
            _pendingUpdate = await mgr.CheckForUpdatesAsync()
                ?? throw new InvalidOperationException("Kein Update verfügbar.");
        }

        // Velopack progress: Action<int> with 0-100 percentage.
        Action<int>? veloProgress = progress is null ? null : pct =>
            progress.Report(new UpdateProgress
            {
                BytesDownloaded  = pct,
                TotalBytes       = 100,
                StageDescription = $"Lade Update… {pct} %"
            });

        // Signature: DownloadUpdatesAsync(UpdateInfo, Action<int>?, CancellationToken)
        await mgr.DownloadUpdatesAsync(_pendingUpdate, veloProgress, ct);

        // Velopack manages its own staging directory.
        // We return a sentinel that ApplyUpdateAndRestart ignores.
        return "velopack_staged";
    }

    public void ApplyUpdateAndRestart(string _stagedPath)
    {
        if (_pendingUpdate is null)
            throw new InvalidOperationException(
                "Kein gestaffeltes Update vorhanden. Zuerst DownloadAndStageUpdateAsync aufrufen.");

        var mgr = CreateManager()
            ?? throw new InvalidOperationException("GitHub-Repository nicht konfiguriert.");

        // Launches the Velopack updater, replaces app files, relaunches new version.
        // This call does not return.
        mgr.ApplyUpdatesAndRestart(_pendingUpdate);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Creates an UpdateManager pointing at the configured GitHub repo.
    /// Returns null when owner/repo are not yet filled in the settings.
    /// </summary>
    private UpdateManager? CreateManager()
    {
        var owner = _settings.Current.GitHubOwner?.Trim();
        var repo  = _settings.Current.GitHubRepo?.Trim();
        if (string.IsNullOrEmpty(owner) || string.IsNullOrEmpty(repo)) return null;

        var source = new GithubSource(
            repoUrl:     $"https://github.com/{owner}/{repo}",
            accessToken: null,   // null = public repo, no auth required
            prerelease:  _settings.Current.IncludePrereleases);

        return new UpdateManager(source);
    }

    private UpdateCheckResult Error(string msg) =>
        new() { CurrentVersion = CurrentVersion, ErrorMessage = msg };

    private static string GetAssemblyVersion()
    {
        var v = Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 1, 0);
        return $"{v.Major}.{v.Minor}.{v.Build}";
    }
}

using OllamaDashboard.Models;

namespace OllamaDashboard.Services;

public interface IUpdateService
{
    /// <summary>Queries GitHub for the latest release and compares with the current assembly version.</summary>
    Task<UpdateCheckResult> CheckForUpdateAsync(CancellationToken ct = default);

    /// <summary>
    /// Downloads the update ZIP, extracts it into a staging folder, and launches
    /// the external Updater.exe which replaces the files after this app exits.
    /// Returns the path to the staged update folder so the caller can inspect/cancel.
    /// </summary>
    Task<string> DownloadAndStageUpdateAsync(
        UpdateCheckResult update,
        IProgress<UpdateProgress>? progress = null,
        CancellationToken ct = default);

    /// <summary>
    /// Launches Updater.exe, which waits for this app to exit and then copies the
    /// staged files over the install folder, finally relaunching the app.
    /// This method calls Application.Current.Shutdown() as the last step.
    /// </summary>
    void ApplyUpdateAndRestart(string stagedUpdateFolder);

    /// <summary>Current app version from the main assembly.</summary>
    string CurrentVersion { get; }
}

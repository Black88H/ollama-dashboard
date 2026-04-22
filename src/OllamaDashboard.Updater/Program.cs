using System.Diagnostics;

namespace OllamaDashboard.Updater;

/// <summary>
/// Tiny helper process spawned by the main app to perform the file swap.
/// Usage:
///   OllamaDashboard.Updater.exe --pid 1234 --source "C:\...\staged" --target "C:\Program Files\OllamaDashboard" --relaunch "C:\...\OllamaDashboard.exe"
/// </summary>
internal static class Program
{
    private const int MaxWaitSeconds = 30;

    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            var opts = ParseArgs(args);
            if (opts is null)
            {
                Log("Ungültige Argumente.");
                return 2;
            }

            Log($"Warte auf Prozess {opts.Pid}…");
            WaitForProcessExit(opts.Pid);

            Log($"Kopiere {opts.Source} → {opts.Target}");
            CopyDirectory(opts.Source, opts.Target);

            Log("Bereinige…");
            TryDeleteDirectory(opts.Source);

            Log("Starte App neu…");
            Process.Start(new ProcessStartInfo
            {
                FileName = opts.Relaunch,
                UseShellExecute = true,
                WorkingDirectory = Path.GetDirectoryName(opts.Relaunch) ?? opts.Target
            });

            return 0;
        }
        catch (Exception ex)
        {
            Log("Fehler: " + ex);
            // Keep a crash log so users can see what happened.
            try
            {
                var crashLog = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "OllamaDashboard", "updater-crash.log");
                Directory.CreateDirectory(Path.GetDirectoryName(crashLog)!);
                File.AppendAllText(crashLog,
                    $"{DateTime.Now:O}\t{ex}\n");
            }
            catch { /* best effort */ }
            return 1;
        }
    }

    private sealed record UpdaterOptions(int Pid, string Source, string Target, string Relaunch);

    private static UpdaterOptions? ParseArgs(string[] args)
    {
        int pid = 0;
        string? source = null, target = null, relaunch = null;
        for (int i = 0; i < args.Length - 1; i++)
        {
            switch (args[i])
            {
                case "--pid":      int.TryParse(args[++i], out pid); break;
                case "--source":   source   = args[++i]; break;
                case "--target":   target   = args[++i]; break;
                case "--relaunch": relaunch = args[++i]; break;
            }
        }
        if (pid <= 0 || source is null || target is null || relaunch is null) return null;
        return new UpdaterOptions(pid, source, target, relaunch);
    }

    private static void WaitForProcessExit(int pid)
    {
        try
        {
            var proc = Process.GetProcessById(pid);
            if (!proc.WaitForExit(MaxWaitSeconds * 1000))
            {
                Log($"Prozess beendet sich nicht innerhalb von {MaxWaitSeconds}s — fahre trotzdem fort.");
            }
        }
        catch (ArgumentException)
        {
            // Process already exited — fine.
        }
        // Extra delay so file handles are definitely released.
        Thread.Sleep(500);
    }

    private static void CopyDirectory(string sourceDir, string targetDir)
    {
        if (!Directory.Exists(sourceDir))
            throw new DirectoryNotFoundException($"Quelle existiert nicht: {sourceDir}");
        Directory.CreateDirectory(targetDir);

        // Some release archives wrap everything in a single top-level folder — unwrap it.
        var entries = Directory.GetFileSystemEntries(sourceDir);
        if (entries.Length == 1 && Directory.Exists(entries[0]))
        {
            sourceDir = entries[0];
        }

        foreach (var src in Directory.EnumerateFiles(sourceDir, "*", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(sourceDir, src);
            var dst = Path.Combine(targetDir, rel);

            // Don't overwrite the updater itself while it's running.
            var dstFileName = Path.GetFileName(dst);
            if (dstFileName.Equals("OllamaDashboard.Updater.exe", StringComparison.OrdinalIgnoreCase))
                continue;

            Directory.CreateDirectory(Path.GetDirectoryName(dst)!);

            // Retry on locked files — Antivirus can briefly hold them.
            for (int attempt = 0; attempt < 5; attempt++)
            {
                try
                {
                    File.Copy(src, dst, overwrite: true);
                    break;
                }
                catch (IOException) when (attempt < 4)
                {
                    Thread.Sleep(400);
                }
            }
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
        }
        catch { /* best effort */ }
    }

    private static void Log(string msg) => Console.WriteLine($"[Updater] {msg}");
}

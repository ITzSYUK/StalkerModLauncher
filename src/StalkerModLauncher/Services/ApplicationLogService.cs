namespace StalkerModLauncher.Services;

public sealed class ApplicationLogService
{
    private const long MaxLogFileBytes = 1024 * 1024;
    private const string LogFileName = "launcher.log";
    private const string PreviousLogFileName = "launcher.old.log";
    private readonly AppPaths _paths;
    private readonly object _sync = new();

    public ApplicationLogService(AppPaths paths)
    {
        _paths = paths;
    }

    public string Write(string message, DateTime? timestamp = null)
    {
        var time = timestamp ?? DateTime.Now;
        try
        {
            lock (_sync)
            {
                Directory.CreateDirectory(_paths.ConfigDirectory);
                var logPath = Path.Combine(_paths.ConfigDirectory, LogFileName);
                RotateIfNeeded(logPath);
                File.AppendAllText(
                    logPath,
                    $"[{time:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}");
            }
        }
        catch
        {
            // File logging is best-effort.
        }

        return $"[{time:HH:mm:ss}] {message}";
    }

    private void RotateIfNeeded(string logPath)
    {
        if (!File.Exists(logPath) || new FileInfo(logPath).Length < MaxLogFileBytes)
        {
            return;
        }

        var previousLogPath = Path.Combine(_paths.ConfigDirectory, PreviousLogFileName);
        if (File.Exists(previousLogPath))
        {
            File.Delete(previousLogPath);
        }

        File.Move(logPath, previousLogPath);
    }
}

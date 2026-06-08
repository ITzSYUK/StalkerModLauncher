namespace StalkerModLauncher.Services;

public sealed class ApplicationLogService
{
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
                File.AppendAllText(
                    Path.Combine(_paths.ConfigDirectory, "launcher.log"),
                    $"[{time:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}");
            }
        }
        catch
        {
            // File logging is best-effort.
        }

        return $"[{time:HH:mm:ss}] {message}";
    }
}

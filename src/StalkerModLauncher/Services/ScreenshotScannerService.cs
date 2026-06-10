using StalkerModLauncher.Models;

namespace StalkerModLauncher.Services;

public sealed class ScreenshotScannerService
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".bmp",
        ".jpeg",
        ".jpg",
        ".png"
    };

    private readonly ProfileDataPathResolver _dataPathResolver;

    public ScreenshotScannerService(ProfileDataPathResolver dataPathResolver)
    {
        _dataPathResolver = dataPathResolver;
    }

    public Task<IReadOnlyList<string>> ScanAsync(
        ModProfile profile,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(() => Scan(profile, cancellationToken), cancellationToken);
    }

    private IReadOnlyList<string> Scan(
        ModProfile profile,
        CancellationToken cancellationToken)
    {
        var directories = new List<string>(_dataPathResolver.GetScreenshotDirectories(profile));
        var gamePath = profile.GameInstallPath;

        if (!string.IsNullOrWhiteSpace(gamePath))
        {
            directories.Add(Path.Combine(gamePath, "userdata", "screenshots"));
            directories.Add(Path.Combine(gamePath, "appdata", "screenshots"));
        }

        var results = new List<string>();
        foreach (var directory in directories.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!Directory.Exists(directory))
            {
                continue;
            }

            try
            {
                foreach (var file in Directory.EnumerateFiles(directory, "*", SearchOption.TopDirectoryOnly))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (SupportedExtensions.Contains(Path.GetExtension(file)))
                    {
                        results.Add(file);
                    }
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // A single unavailable screenshot directory must not prevent viewing the others.
            }
        }

        results.Sort(StringComparer.OrdinalIgnoreCase);
        return results;
    }
}

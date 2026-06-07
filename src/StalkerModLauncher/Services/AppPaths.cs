namespace StalkerModLauncher.Services;

public sealed class AppPaths
{
    private readonly bool _preferGameDriveWorkspace;

    public AppPaths()
        : this(
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "StalkerModLauncher"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "StalkerModLauncher", "Workspaces"))
    {
    }

    public AppPaths(string configDirectory, string workspaceRoot, bool preferGameDriveWorkspace = true)
    {
        ConfigDirectory = configDirectory;
        SettingsFile = Path.Combine(ConfigDirectory, "settings.json");
        WorkspaceRoot = workspaceRoot;
        _preferGameDriveWorkspace = preferGameDriveWorkspace;
    }

    public string ConfigDirectory { get; }
    public string SettingsFile { get; }
    public string WorkspaceRoot { get; }

    public string GetPreferredWorkspaceRoot(string? gameInstallPath)
    {
        if (!_preferGameDriveWorkspace)
        {
            return WorkspaceRoot;
        }

        if (!string.IsNullOrWhiteSpace(gameInstallPath))
        {
            try
            {
                var root = Path.GetPathRoot(Path.GetFullPath(gameInstallPath));
                if (!string.IsNullOrWhiteSpace(root))
                {
                    return Path.Combine(root, "StalkerModLauncher", "Workspaces");
                }
            }
            catch
            {
                return WorkspaceRoot;
            }
        }

        return WorkspaceRoot;
    }

    public IReadOnlyList<string> GetManagedWorkspaceRoots(string? gameInstallPath)
    {
        return new[] { WorkspaceRoot, GetPreferredWorkspaceRoot(gameInstallPath) }
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}

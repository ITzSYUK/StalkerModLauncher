namespace StalkerModLauncher.Services;

public sealed class AppPaths
{
    public AppPaths()
        : this(
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "StalkerModLauncher"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "StalkerModLauncher", "Workspaces"))
    {
    }

    public AppPaths(string configDirectory, string workspaceRoot)
    {
        ConfigDirectory = configDirectory;
        SettingsFile = Path.Combine(ConfigDirectory, "settings.json");
        WorkspaceRoot = workspaceRoot;
    }

    public string ConfigDirectory { get; }
    public string SettingsFile { get; }
    public string WorkspaceRoot { get; }

    public string GetPreferredWorkspaceRoot(string? gameInstallPath)
    {
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

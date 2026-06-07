namespace StalkerModLauncher.Services;

public sealed class AppPaths
{
    public AppPaths()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        ConfigDirectory = Path.Combine(appData, "StalkerModLauncher");
        SettingsFile = Path.Combine(ConfigDirectory, "settings.json");
        WorkspaceRoot = Path.Combine(localAppData, "StalkerModLauncher", "Workspaces");
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

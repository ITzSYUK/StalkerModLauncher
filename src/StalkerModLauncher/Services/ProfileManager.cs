using StalkerModLauncher.Models;

namespace StalkerModLauncher.Services;

public sealed class ProfileManager
{
    private readonly AppPaths _paths;
    private readonly IProfileWorkspaceManager _workspaceManager;

    public ProfileManager(AppPaths paths, IProfileWorkspaceManager workspaceManager)
    {
        _paths = paths;
        _workspaceManager = workspaceManager;
    }

    public ModProfile Create(IReadOnlyCollection<ModProfile> profiles, string gameInstallPath)
    {
        var profile = new ModProfile
        {
            Name = GetUniqueName(profiles, $"Profile {profiles.Count + 1}"),
            Description = "S.T.A.L.K.E.R. mod profile",
            GameInstallPath = gameInstallPath
        };
        profile.WorkspacePath = CreateWorkspacePath(profile, gameInstallPath);
        return profile;
    }

    public ModProfile Duplicate(IReadOnlyCollection<ModProfile> profiles, ModProfile source)
    {
        var duplicate = new ModProfile
        {
            Name = GetUniqueName(profiles, $"{source.Name} — копия"),
            Description = source.Description,
            IsEnabled = source.IsEnabled,
            IsStandalone = source.IsStandalone,
            LaunchArguments = source.LaunchArguments,
            ExecutableRelativePath = source.ExecutableRelativePath,
            WorkingDirectoryRelative = source.WorkingDirectoryRelative,
            ConfigNotes = source.ConfigNotes,
            GameInstallPath = source.GameInstallPath
        };

        foreach (var sourceMod in source.Mods.OrderBy(mod => mod.Order))
        {
            duplicate.Mods.Add(new ModEntry
            {
                Name = sourceMod.Name,
                SourcePath = sourceMod.SourcePath,
                IsEnabled = sourceMod.IsEnabled,
                Order = duplicate.Mods.Count + 1,
                Notes = sourceMod.Notes
            });
        }

        if (!duplicate.IsStandalone)
        {
            duplicate.WorkspacePath = CreateWorkspacePath(duplicate, duplicate.GameInstallPath);
        }

        return duplicate;
    }

    public void PrepareImported(IReadOnlyCollection<ModProfile> profiles, ModProfile profile)
    {
        EnsureDefaults(profile);
        profile.Name = GetUniqueName(profiles, profile.Name);
    }

    public void EnsureDefaults(ModProfile profile)
    {
        profile.IsRunning = false;

        if (string.IsNullOrWhiteSpace(profile.Id))
        {
            profile.Id = Guid.NewGuid().ToString("N");
        }

        if (string.IsNullOrWhiteSpace(profile.ExecutableRelativePath))
        {
            profile.ExecutableRelativePath = @"bin\xr_3da.exe";
        }
    }

    public ModProfile? Delete(ICollection<ModProfile> profiles, ModProfile profile)
    {
        if (!profile.IsStandalone)
        {
            _workspaceManager.DeleteProfileWorkspace(profile, profile.GameInstallPath);
        }

        profiles.Remove(profile);
        return profiles.FirstOrDefault();
    }

    public static string GetUniqueName(IEnumerable<ModProfile> profiles, string requestedName)
    {
        var profileList = profiles.ToList();
        var baseName = string.IsNullOrWhiteSpace(requestedName)
            ? $"Profile {profileList.Count + 1}"
            : requestedName.Trim();
        var name = baseName;
        var counter = 1;
        while (profileList.Any(profile => profile.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
        {
            name = $"{baseName} ({++counter})";
        }

        return name;
    }

    private string CreateWorkspacePath(ModProfile profile, string gameInstallPath)
    {
        var readableName = FileSystemSafety.SanitizeName(profile.Name);
        if (readableName.Length > 80)
        {
            readableName = readableName[..80].TrimEnd(' ', '.');
        }

        var shortId = profile.Id.Length > 8 ? profile.Id[..8] : profile.Id;
        return Path.Combine(_paths.GetPreferredWorkspaceRoot(gameInstallPath), $"{readableName}-{shortId}");
    }
}

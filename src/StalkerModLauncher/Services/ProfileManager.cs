using StalkerModLauncher.Models;
using System.Collections.ObjectModel;

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

    public ModProfile Create(IReadOnlyCollection<ModProfile> profiles)
    {
        return new ModProfile
        {
            Name = GetUniqueName(profiles, $"Profile {profiles.Count + 1}"),
            Description = "S.T.A.L.K.E.R. mod profile"
        };
    }

    public ModProfile Duplicate(IReadOnlyCollection<ModProfile> profiles, ModProfile source)
    {
        var duplicate = new ModProfile
        {
            Name = GetUniqueName(profiles, $"{source.Name} — копия"),
            Description = source.Description,
            IsEnabled = source.IsEnabled,
            IsDiscordStatusEnabled = source.IsDiscordStatusEnabled,
            IsStandalone = source.IsStandalone,
            LaunchBackendKind = source.LaunchBackendKind,
            LaunchArguments = source.LaunchArguments,
            ExecutableRelativePath = source.ExecutableRelativePath,
            ExecutableSourcePath = source.ExecutableSourcePath,
            UsvfsExecutableOverrideRelativePath = source.UsvfsExecutableOverrideRelativePath,
            WorkingDirectoryRelative = source.WorkingDirectoryRelative,
            GameInstallPath = source.GameInstallPath
        };

        foreach (var sourceMod in source.Mods.OrderBy(mod => mod.Order))
        {
            duplicate.Mods.Add(new ModEntry
            {
                Name = sourceMod.Name,
                SourcePath = sourceMod.SourcePath,
                IsEnabled = sourceMod.IsEnabled,
                Order = duplicate.Mods.Count + 1
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

    public bool MoveToInsertionIndex(
        ObservableCollection<ModProfile> profiles,
        ModProfile profile,
        int insertionIndex)
    {
        return CollectionReorderer.MoveToInsertionIndex(profiles, profile, insertionIndex);
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

    public string? GetProfileFolderPath(ModProfile profile)
    {
        if (profile.IsStandalone)
        {
            return profile.Mods
                .FirstOrDefault(mod => mod.IsEnabled && Directory.Exists(mod.SourcePath))
                ?.SourcePath;
        }

        if (!string.IsNullOrWhiteSpace(profile.WorkspacePath))
        {
            return profile.WorkspacePath;
        }

        return CreateWorkspacePath(profile, profile.GameInstallPath);
    }

    public string EnsureProfileFolderPath(ModProfile profile, IProgress<string>? progress = null)
    {
        if (profile.IsStandalone)
        {
            throw new InvalidOperationException("Автономный профиль не использует workspace.");
        }

        var workspacePath = _workspaceManager.EnsureProfileWorkspace(
            profile,
            profile.GameInstallPath,
            progress);
        profile.WorkspacePath = workspacePath;
        return workspacePath;
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
        return Path.Combine(_paths.GetPreferredWorkspaceRoot(gameInstallPath), CreateWorkspaceDirectoryName(profile));
    }

    internal static string CreateWorkspaceDirectoryName(ModProfile profile)
    {
        var readableName = FileSystemSafety.SanitizeName(profile.Name);
        if (readableName.Length > 80)
        {
            readableName = readableName[..80].TrimEnd(' ', '.');
        }

        var shortId = profile.Id.Length > 8 ? profile.Id[..8] : profile.Id;
        return $"{readableName}-{shortId}";
    }
}

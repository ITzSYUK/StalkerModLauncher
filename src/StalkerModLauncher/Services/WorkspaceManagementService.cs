using System.Text.Json;
using StalkerModLauncher.Models;

namespace StalkerModLauncher.Services;

public sealed class WorkspaceManagementService
{
    private readonly WorkspaceBuilder _workspaceBuilder;

    public WorkspaceManagementService(WorkspaceBuilder workspaceBuilder)
    {
        _workspaceBuilder = workspaceBuilder;
    }

    public Task<WorkspaceStatus> InspectAsync(ModProfile profile, CancellationToken cancellationToken = default)
    {
        return Task.Run(() => Inspect(profile, cancellationToken), cancellationToken);
    }

    public void ClearCache(ModProfile profile)
    {
        _workspaceBuilder.ClearProfileWorkspaceCache(profile, profile.GameInstallPath);
    }

    public async Task RebuildAsync(ModProfile profile, IProgress<string> progress, CancellationToken cancellationToken = default)
    {
        ClearCache(profile);
        var result = await _workspaceBuilder.BuildAsync(profile.GameInstallPath, profile, progress, cancellationToken);
        profile.WorkspacePath = result.ProfileWorkspacePath;
        profile.ExecutableRelativePath = result.ExecutableRelativePath;
        profile.WorkingDirectoryRelative = result.WorkingDirectoryRelative;
    }

    public Task MoveAsync(
        ModProfile profile,
        string destinationRoot,
        IProgress<string> progress,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(() => Move(profile, destinationRoot, progress, cancellationToken), cancellationToken);
    }

    private void Move(ModProfile profile, string destinationRoot, IProgress<string> progress, CancellationToken cancellationToken)
    {
        if (profile.IsStandalone)
        {
            throw new InvalidOperationException("Автономный профиль не использует workspace.");
        }

        Directory.CreateDirectory(destinationRoot);
        File.WriteAllText(
            Path.Combine(destinationRoot, WorkspaceBuilder.RootMarkerFileName),
            "Managed workspace root created by Stalker Mod Launcher.");
        var destination = Path.Combine(destinationRoot, ProfileManager.CreateWorkspaceDirectoryName(profile));
        var oldWorkspace = profile.WorkspacePath;
        if (!string.IsNullOrWhiteSpace(oldWorkspace) && FileSystemSafety.IsSameDirectory(oldWorkspace, destination))
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(oldWorkspace) &&
            (FileSystemSafety.IsDirectoryInside(destination, oldWorkspace) ||
             FileSystemSafety.IsDirectoryInside(oldWorkspace, destination)))
        {
            throw new InvalidOperationException("Новая папка workspace не должна находиться внутри старой папки или содержать её.");
        }

        if (Directory.Exists(destination))
        {
            throw new InvalidOperationException($"Папка назначения уже существует: {destination}");
        }

        var temporary = destination + $".moving-{Guid.NewGuid():N}";
        Directory.CreateDirectory(temporary);
        try
        {
            File.WriteAllText(
                Path.Combine(temporary, ".stalker-launcher-workspace"),
                "Managed by Stalker Mod Launcher. It is safe for the launcher to recreate the 'current' subfolder.");

            var sourceUserData = string.IsNullOrWhiteSpace(oldWorkspace) ? string.Empty : Path.Combine(oldWorkspace, "userdata");
            var targetUserData = Path.Combine(temporary, "userdata");
            if (Directory.Exists(sourceUserData))
            {
                progress.Report("Копирование сохранений, настроек и логов...");
                CopyDirectory(sourceUserData, targetUserData, cancellationToken);
            }

            cancellationToken.ThrowIfCancellationRequested();
            Directory.Move(temporary, destination);
            profile.WorkspacePath = destination;
            progress.Report($"Workspace перенесён: {destination}. Папка current будет пересобрана.");

            if (!string.IsNullOrWhiteSpace(oldWorkspace) && Directory.Exists(oldWorkspace))
            {
                var oldProfile = new ModProfile
                {
                    WorkspacePath = oldWorkspace,
                    GameInstallPath = profile.GameInstallPath
                };
                _workspaceBuilder.DeleteProfileWorkspace(oldProfile, profile.GameInstallPath);
            }
        }
        catch
        {
            if (Directory.Exists(temporary))
            {
                Directory.Delete(temporary, recursive: true);
            }

            throw;
        }
    }

    private static void CopyDirectory(string source, string destination, CancellationToken cancellationToken)
    {
        foreach (var directory in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();
            Directory.CreateDirectory(Path.Combine(destination, Path.GetRelativePath(source, directory)));
        }

        Directory.CreateDirectory(destination);
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var target = Path.Combine(destination, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite: false);
        }
    }

    private static WorkspaceStatus Inspect(ModProfile profile, CancellationToken cancellationToken)
    {
        var workspace = profile.WorkspacePath;
        var current = string.IsNullOrWhiteSpace(workspace) ? string.Empty : Path.Combine(workspace, "current");
        if (string.IsNullOrWhiteSpace(current) || !Directory.Exists(current))
        {
            return WorkspaceStatus.Missing(workspace);
        }

        cancellationToken.ThrowIfCancellationRequested();
        var manifest = ReadManifest(workspace);
        return manifest is null
            ? new WorkspaceStatus(workspace, true, 0, 0, 0, 0, 0, 0, null, false)
            : new WorkspaceStatus(
                workspace,
                true,
                manifest.LogicalSizeBytes,
                manifest.PhysicalSizeBytes,
                manifest.FileCount,
                manifest.SymbolicLinkCount,
                manifest.HardLinkCount,
                manifest.LocalFileCount,
                manifest.BuiltAtUtc,
                manifest.HasStatistics);
    }

    private static WorkspaceBuildManifest? ReadManifest(string workspace)
    {
        try
        {
            return JsonSerializer.Deserialize<WorkspaceBuildManifest>(
                File.ReadAllText(Path.Combine(workspace, "build-manifest.json")));
        }
        catch
        {
            return null;
        }
    }
}

using System.IO;
using StalkerModLauncher.Models;

namespace StalkerModLauncher.Services;

public interface IProfileWorkspaceManager
{
    void DeleteProfileWorkspace(ModProfile profile, string gamePath);
    void ClearProfileWorkspaceCache(ModProfile profile, string gamePath);
}

public sealed class WorkspaceBuilder : IProfileWorkspaceManager
{
    private const string MarkerFileName = ".stalker-launcher-workspace";
    internal const string RootMarkerFileName = ".stalker-launcher-workspace-root";
    private const string ManifestFileName = "build-manifest.json";
    private const string WorkspaceFormatVersion = "strict-links-v1";
    private readonly AppPaths _paths;
    private readonly WorkspaceSourceScanner _sourceScanner = new();
    private readonly WorkspaceManifestStore _manifestStore = new();
    private readonly WorkspaceMaterializer _materializer = new();
    private readonly WorkspaceExecutableResolver _executableResolver = new();
    private readonly ProfileDataConfigurator _dataConfigurator = new();
    private static EnumerationOptions SafeEnumerationOptions { get; } = new()
    {
        RecurseSubdirectories = true,
        IgnoreInaccessible = false,
        AttributesToSkip = FileAttributes.ReparsePoint
    };

    public WorkspaceBuilder(AppPaths paths)
    {
        _paths = paths;
    }

    public Task<WorkspaceBuildResult> BuildAsync(
        string gamePath,
        ModProfile profile,
        IProgress<string> progress,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(() => Build(gamePath, profile, progress, cancellationToken), cancellationToken);
    }

    private WorkspaceBuildResult Build(
        string gamePath,
        ModProfile profile,
        IProgress<string> progress,
        CancellationToken cancellationToken)
    {
        FileSystemSafety.EnsureRelativePath(profile.ExecutableRelativePath, "Launch executable");

        if (profile.IsStandalone)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return BuildStandalone(profile, gamePath, progress, cancellationToken);
        }

        if (string.IsNullOrWhiteSpace(gamePath))
        {
            throw new InvalidOperationException("Для неавтономного профиля выберите папку игры.");
        }

        if (!Directory.Exists(gamePath))
        {
            throw new DirectoryNotFoundException($"Game folder was not found: {gamePath}");
        }

        var workspaceRoot = EnsureProfileWorkspace(profile, gamePath);
        var currentWorkspace = Path.Combine(workspaceRoot, "current");
        FileSystemSafety.EnsureDirectoryInside(currentWorkspace, workspaceRoot);
        progress.Report("Checking game and mod files...");
        var sourceSnapshot = _sourceScanner.Capture(gamePath, profile, cancellationToken);
        var buildSignature = _sourceScanner.CreateBuildSignature(WorkspaceFormatVersion, profile, sourceSnapshot);
        var cachedExecutable = _manifestStore.TryGetCachedExecutable(workspaceRoot, currentWorkspace, profile, buildSignature, progress);
        if (cachedExecutable is not null)
        {
            return new WorkspaceBuildResult(
                currentWorkspace,
                cachedExecutable,
                workspaceRoot,
                profile.ExecutableRelativePath,
                profile.WorkingDirectoryRelative);
        }

        _materializer.ValidateLinkSupport(sourceSnapshot, workspaceRoot, progress);

        progress.Report("Preparing clean profile workspace...");
        FileSystemSafety.DeleteDirectoryContents(currentWorkspace, workspaceRoot);
        Directory.CreateDirectory(currentWorkspace);

        var stats = new WorkspaceBuildStats();

        progress.Report("Linking base game into isolated workspace...");
        _materializer.MirrorBaseGame(sourceSnapshot.Game, currentWorkspace, progress, stats, cancellationToken);

        foreach (var mod in profile.Mods.Where(mod => mod.IsEnabled).OrderBy(mod => mod.Order))
        {
            cancellationToken.ThrowIfCancellationRequested();
            _materializer.ApplyMod(currentWorkspace, mod, sourceSnapshot.Mods[mod.Id], progress, stats, cancellationToken);
        }

        var workingDirectoryRelative = _dataConfigurator.Configure(gamePath, currentWorkspace, workspaceRoot, progress);
        ApplyPinnedExecutableSource(profile, currentWorkspace, progress, stats);

        var executablePath = Path.Combine(currentWorkspace, profile.ExecutableRelativePath);
        var executableRelativePath = profile.ExecutableRelativePath;
        if (!File.Exists(executablePath))
        {
            var detectedExecutable = _executableResolver.Resolve(currentWorkspace, profile.ExecutableRelativePath, progress);
            if (detectedExecutable is null)
            {
                var discovered = Directory.EnumerateFiles(currentWorkspace, "*.exe", SafeEnumerationOptions)
                    .Select(path => Path.GetRelativePath(currentWorkspace, path))
                    .Order(StringComparer.OrdinalIgnoreCase)
                    .Take(12)
                    .ToArray();
                var details = discovered.Length == 0
                    ? "No executable files were found in the built workspace."
                    : $"Executables found: {string.Join(", ", discovered)}";

                throw new FileNotFoundException(
                    $"Profile executable was not found after workspace build: {profile.ExecutableRelativePath}. {details}",
                    executablePath);
            }

            executablePath = detectedExecutable.FullPath;
            executableRelativePath = detectedExecutable.RelativePath;
        }

        progress.Report($"Workspace is ready. Hard links: {stats.LinkedFiles:N0}, symbolic links: {stats.SymbolicLinkedFiles:N0}, profile-local copies: {stats.ProtectedCopies:N0}.");
        _manifestStore.Write(workspaceRoot, buildSignature, stats);
        return new WorkspaceBuildResult(
            currentWorkspace,
            executablePath,
            workspaceRoot,
            executableRelativePath,
            workingDirectoryRelative);
    }

    private void ApplyPinnedExecutableSource(
        ModProfile profile,
        string currentWorkspace,
        IProgress<string> progress,
        WorkspaceBuildStats stats)
    {
        var sourceRoot = ProfileExecutableSourceResolver.FindPinnedSourceRoot(profile);
        if (sourceRoot is null)
        {
            if (!string.IsNullOrWhiteSpace(profile.ExecutableSourcePath))
            {
                throw new InvalidOperationException(
                    "Ручной источник бинарника больше недоступен. Выберите файл запуска заново или сбросьте источник на автоматический.");
            }

            return;
        }

        var sourceFile = FileSystemSafety.ResolvePathInside(
            sourceRoot.RootPath,
            profile.ExecutableRelativePath,
            "Pinned launch executable");
        if (!File.Exists(sourceFile))
        {
            throw new FileNotFoundException(
                $"Ручной источник бинарника найден, но файл отсутствует: {sourceFile}",
                sourceFile);
        }

        _materializer.ReplaceFile(sourceFile, currentWorkspace, profile.ExecutableRelativePath, stats);
        progress.Report($"Используется вручную выбранный бинарник: {profile.ExecutableRelativePath}. Источник: {sourceRoot.DisplayName}.");
    }

    public string GetSavedGamesPath(ModProfile profile)
    {
        if (string.IsNullOrWhiteSpace(profile.WorkspacePath))
        {
            return string.Empty;
        }

        return Path.Combine(profile.WorkspacePath, "userdata", "savedgames");
    }

    public void DeleteProfileWorkspace(ModProfile profile, string gamePath)
    {
        if (string.IsNullOrWhiteSpace(profile.WorkspacePath) || !Directory.Exists(profile.WorkspacePath))
        {
            return;
        }

        var workspacePath = Path.GetFullPath(profile.WorkspacePath);
        var allowedRoot = FindAllowedWorkspaceParent(workspacePath, gamePath);

        if (allowedRoot is null)
        {
            throw new InvalidOperationException($"Refusing to delete workspace outside managed launcher roots: {workspacePath}");
        }

        var markerPath = Path.Combine(workspacePath, MarkerFileName);
        if (!File.Exists(markerPath))
        {
            throw new InvalidOperationException($"Refusing to delete profile workspace without launcher marker file: {workspacePath}");
        }

        FileSystemSafety.DeleteDirectoryContents(workspacePath, allowedRoot);
    }

    public void ClearProfileWorkspaceCache(ModProfile profile, string gamePath)
    {
        if (string.IsNullOrWhiteSpace(profile.WorkspacePath) || !Directory.Exists(profile.WorkspacePath))
        {
            return;
        }

        var workspacePath = Path.GetFullPath(profile.WorkspacePath);
        var allowedRoot = FindAllowedWorkspaceParent(workspacePath, gamePath);
        if (allowedRoot is null || !File.Exists(Path.Combine(workspacePath, MarkerFileName)))
        {
            throw new InvalidOperationException("Лаунчер отказался очищать папку без защитного маркера workspace.");
        }

        var current = Path.Combine(workspacePath, "current");
        if (Directory.Exists(current))
        {
            FileSystemSafety.DeleteDirectoryContents(current, workspacePath);
        }

        var manifest = Path.Combine(workspacePath, ManifestFileName);
        if (File.Exists(manifest))
        {
            File.Delete(manifest);
        }
    }

    private string EnsureProfileWorkspace(ModProfile profile, string gamePath)
    {
        var preferredRoot = _paths.GetPreferredWorkspaceRoot(gamePath);
        var managedRoots = _paths.GetManagedWorkspaceRoots(gamePath);

        var generatedWorkspacePath = Path.Combine(preferredRoot, ProfileManager.CreateWorkspaceDirectoryName(profile));
        var workspacePath = string.IsNullOrWhiteSpace(profile.WorkspacePath) ||
                            ShouldRefreshUnusedGeneratedPath(profile, managedRoots)
            ? generatedWorkspacePath
            : profile.WorkspacePath;

        var hasCustomWorkspaceMarker = !string.IsNullOrWhiteSpace(profile.WorkspacePath) &&
                                       File.Exists(Path.Combine(profile.WorkspacePath, MarkerFileName)) &&
                                       HasManagedParentMarker(profile.WorkspacePath);
        if (!hasCustomWorkspaceMarker &&
            !string.IsNullOrWhiteSpace(profile.WorkspacePath) &&
            !string.IsNullOrWhiteSpace(gamePath) &&
            !FileSystemSafety.IsSameDirectory(Path.GetPathRoot(workspacePath)!, Path.GetPathRoot(preferredRoot)!))
        {
            workspacePath = Path.Combine(preferredRoot, $"{FileSystemSafety.SanitizeName(profile.Name)}-{profile.Id}");
        }

        if (!hasCustomWorkspaceMarker && !managedRoots.Any(root =>
                FileSystemSafety.IsDirectoryInside(workspacePath, root) &&
                !FileSystemSafety.IsSameDirectory(workspacePath, root)))
        {
            throw new InvalidOperationException($"Profile workspace must be a profile-specific folder inside a managed launcher workspace root: {workspacePath}");
        }

        Directory.CreateDirectory(workspacePath);

        var markerPath = Path.Combine(workspacePath, MarkerFileName);
        if (!File.Exists(markerPath))
        {
            File.WriteAllText(markerPath, "Managed by Stalker Mod Launcher. It is safe for the launcher to recreate the 'current' subfolder.");
        }

        return workspacePath;
    }

    private string? FindAllowedWorkspaceParent(string workspacePath, string gamePath)
    {
        var managedRoot = _paths.GetManagedWorkspaceRoots(gamePath)
            .Select(Path.GetFullPath)
            .FirstOrDefault(root =>
                FileSystemSafety.IsDirectoryInside(workspacePath, root) &&
                !FileSystemSafety.IsSameDirectory(workspacePath, root));
        if (managedRoot is not null)
        {
            return managedRoot;
        }

        if (!File.Exists(Path.Combine(workspacePath, MarkerFileName)) || !HasManagedParentMarker(workspacePath))
        {
            return null;
        }

        return Directory.GetParent(workspacePath)?.FullName;
    }

    private static bool HasManagedParentMarker(string workspacePath)
    {
        var parent = Directory.GetParent(Path.GetFullPath(workspacePath))?.FullName;
        return parent is not null && File.Exists(Path.Combine(parent, RootMarkerFileName));
    }

    private static bool ShouldRefreshUnusedGeneratedPath(ModProfile profile, IReadOnlyList<string> managedRoots)
    {
        if (string.IsNullOrWhiteSpace(profile.WorkspacePath) || Directory.Exists(profile.WorkspacePath))
        {
            return false;
        }

        var workspacePath = Path.GetFullPath(profile.WorkspacePath);
        if (!managedRoots.Any(root => FileSystemSafety.IsDirectoryInside(workspacePath, root)))
        {
            return false;
        }

        var directoryName = Path.GetFileName(workspacePath);
        var shortId = profile.Id.Length > 8 ? profile.Id[..8] : profile.Id;
        return directoryName.EndsWith($"-{shortId}", StringComparison.OrdinalIgnoreCase) ||
               directoryName.EndsWith($"-{profile.Id}", StringComparison.OrdinalIgnoreCase);
    }

    private WorkspaceBuildResult BuildStandalone(
        ModProfile profile,
        string gamePath,
        IProgress<string> progress,
        CancellationToken cancellationToken)
    {
        var modRoot = profile.Mods.FirstOrDefault(m => m.IsEnabled && Directory.Exists(m.SourcePath))?.SourcePath;
        if (modRoot is null)
        {
            throw new InvalidOperationException(
                "Standalone profile has no enabled mod with a valid folder. Add a mod and enable it.");
        }

        modRoot = Path.GetFullPath(modRoot);
        var exePath = FileSystemSafety.ResolvePathInside(modRoot, profile.ExecutableRelativePath, "Launch executable");
        var executableRelativePath = profile.ExecutableRelativePath;

        if (!File.Exists(exePath))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var found = _executableResolver.ResolveStandalone(modRoot, profile.ExecutableRelativePath, cancellationToken);

            if (found is null)
            {
                throw new FileNotFoundException(
                    $"No executable found in standalone mod folder: {modRoot}", exePath);
            }

            executableRelativePath = found.RelativePath;
            exePath = found.FullPath;
            progress.Report($"Бинарник профиля не найден. Автоматически выбран '{found.RelativePath}': {found.Reason}.");
        }

        var workingDirectoryRelative = profile.WorkingDirectoryRelative;
        var fsgameDir = _dataConfigurator.FindFileDirectory(modRoot, "fsgame.ltx");
        if (fsgameDir is not null)
        {
            var relativeDir = Path.GetRelativePath(modRoot, fsgameDir);
            if (relativeDir != ".")
            {
                workingDirectoryRelative = relativeDir;
            }
        }

        progress.Report($"Standalone mod ready: {profile.Name}");
        return new WorkspaceBuildResult(
            modRoot,
            exePath,
            profile.WorkspacePath,
            executableRelativePath,
            workingDirectoryRelative);
    }
}

public sealed record WorkspaceBuildResult(
    string WorkspaceRoot,
    string ExecutablePath,
    string ProfileWorkspacePath,
    string ExecutableRelativePath,
    string WorkingDirectoryRelative);

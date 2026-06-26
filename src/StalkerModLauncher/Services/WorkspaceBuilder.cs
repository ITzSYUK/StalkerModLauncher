using System.IO;
using System.Diagnostics;
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

        var totalTimer = Stopwatch.StartNew();
        var workspaceRoot = EnsureProfileWorkspace(profile, gamePath, progress);
        var currentWorkspace = Path.Combine(workspaceRoot, "current");
        FileSystemSafety.EnsureDirectoryInside(currentWorkspace, workspaceRoot);
        progress.Report("Проверка файлов игры и модов...");
        var scanTimer = Stopwatch.StartNew();
        var sourceSnapshot = _sourceScanner.Capture(gamePath, profile, cancellationToken);
        var buildSignature = _sourceScanner.CreateBuildSignature(WorkspaceFormatVersion, profile, sourceSnapshot);
        scanTimer.Stop();
        var cachedExecutable = _manifestStore.TryGetCachedExecutable(workspaceRoot, currentWorkspace, profile, buildSignature, progress);
        if (cachedExecutable is not null)
        {
            progress.Report($"Проверка источников: {FormatElapsed(scanTimer.Elapsed)}. Пересборка не требуется.");
            return new WorkspaceBuildResult(
                currentWorkspace,
                cachedExecutable,
                workspaceRoot,
                profile.ExecutableRelativePath,
                profile.WorkingDirectoryRelative);
        }

        _materializer.ValidateLinkSupport(sourceSnapshot, workspaceRoot, progress);

        progress.Report("Подготовка чистой рабочей среды профиля...");
        var cleanupTimer = Stopwatch.StartNew();
        _materializer.DeleteWorkspaceContents(currentWorkspace, workspaceRoot, () => sourceSnapshot, progress);
        Directory.CreateDirectory(currentWorkspace);
        cleanupTimer.Stop();

        var stats = new WorkspaceBuildStats();

        progress.Report("Подключение базовой игры к рабочей среде...");
        var baseGameTimer = Stopwatch.StartNew();
        _materializer.MirrorBaseGame(sourceSnapshot.Game, currentWorkspace, progress, stats, cancellationToken);
        baseGameTimer.Stop();

        var enabledMods = profile.Mods.Where(mod => mod.IsEnabled).OrderBy(mod => mod.Order).ToArray();
        var modsTimer = Stopwatch.StartNew();
        foreach (var mod in enabledMods)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _materializer.ApplyMod(currentWorkspace, mod, sourceSnapshot.Mods[mod.Id], progress, stats, cancellationToken);
        }
        modsTimer.Stop();

        var configurationTimer = Stopwatch.StartNew();
        var workingDirectoryRelative = _dataConfigurator.Configure(gamePath, currentWorkspace, workspaceRoot, progress);
        ApplyPinnedExecutableSource(profile, currentWorkspace, progress, stats);
        configurationTimer.Stop();

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

        progress.Report($"Рабочая среда готова. Hard link: {stats.LinkedFiles:N0}; symbolic link: {stats.SymbolicLinkedFiles:N0}; локальных файлов: {stats.ProtectedCopies:N0}.");
        if (stats.ReadOnlyHandledFiles > 0)
        {
            progress.Report($"Файлы «только чтение»: {stats.ReadOnlyHandledFiles:N0}. Символические ссылки: {stats.ReadOnlySymbolicLinkedFiles:N0}; независимые копии: {stats.ReadOnlyCopiedFiles:N0}. Исходные файлы модов не изменены.");
        }

        totalTimer.Stop();
        progress.Report(
            $"Время подготовки: проверка {FormatElapsed(scanTimer.Elapsed)}; очистка {FormatElapsed(cleanupTimer.Elapsed)}; " +
            $"игра {FormatElapsed(baseGameTimer.Elapsed)}; моды {FormatElapsed(modsTimer.Elapsed)}; " +
            $"настройка {FormatElapsed(configurationTimer.Elapsed)}; всего {FormatElapsed(totalTimer.Elapsed)}.");
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
            RestoreGeneratedWorkspaceMarker(profile, workspacePath, allowedRoot);
        }

        if (!File.Exists(markerPath))
        {
            throw new InvalidOperationException($"Refusing to delete profile workspace without launcher marker file: {workspacePath}");
        }

        _materializer.DeleteWorkspaceContents(
            workspacePath,
            allowedRoot,
            () => _sourceScanner.Capture(gamePath, profile, CancellationToken.None));
    }

    public void ClearProfileWorkspaceCache(ModProfile profile, string gamePath)
    {
        ClearProfileWorkspaceCache(profile, gamePath, progress: null);
    }

    public void ClearProfileWorkspaceCache(
        ModProfile profile,
        string gamePath,
        IProgress<string>? progress)
    {
        if (string.IsNullOrWhiteSpace(profile.WorkspacePath) || !Directory.Exists(profile.WorkspacePath))
        {
            return;
        }

        var workspacePath = Path.GetFullPath(profile.WorkspacePath);
        var allowedRoot = FindAllowedWorkspaceParent(workspacePath, gamePath);
        if (allowedRoot is null)
        {
            throw new InvalidOperationException("Лаунчер отказался очищать папку без защитного маркера workspace.");
        }

        if (RestoreGeneratedWorkspaceMarker(profile, workspacePath, allowedRoot))
        {
            progress?.Report("Восстановлен защитный маркер профиля. Рабочую папку снова можно безопасно очищать.");
        }
        if (!File.Exists(Path.Combine(workspacePath, MarkerFileName)))
        {
            throw new InvalidOperationException("Лаунчер отказался очищать папку без защитного маркера workspace.");
        }

        var current = Path.Combine(workspacePath, "current");
        if (Directory.Exists(current))
        {
            _materializer.DeleteWorkspaceContents(
                current,
                workspacePath,
                () => _sourceScanner.Capture(gamePath, profile, CancellationToken.None));
        }

        var manifest = Path.Combine(workspacePath, ManifestFileName);
        if (File.Exists(manifest))
        {
            File.Delete(manifest);
        }
    }

    private string EnsureProfileWorkspace(ModProfile profile, string gamePath, IProgress<string> progress)
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

        var workspaceAlreadyExisted = Directory.Exists(workspacePath);
        var workspaceRootAlreadyExisted = Directory.Exists(preferredRoot);
        Directory.CreateDirectory(workspacePath);
        if (EnsureWorkspaceRootMarker(preferredRoot) && workspaceRootAlreadyExisted)
        {
            progress.Report("Восстановлен служебный маркер корня workspace.");
        }

        var markerPath = Path.Combine(workspacePath, MarkerFileName);
        if (!File.Exists(markerPath))
        {
            WriteWorkspaceMarker(markerPath);
            if (workspaceAlreadyExisted)
            {
                progress.Report("Восстановлен защитный маркер профиля. Рабочую папку снова можно безопасно пересобирать.");
            }
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

    private static bool RestoreGeneratedWorkspaceMarker(
        ModProfile profile,
        string workspacePath,
        string managedRoot)
    {
        var expectedWorkspace = Path.Combine(managedRoot, ProfileManager.CreateWorkspaceDirectoryName(profile));
        if (!FileSystemSafety.IsSameDirectory(workspacePath, expectedWorkspace))
        {
            return false;
        }

        Directory.CreateDirectory(managedRoot);
        var rootMarkerRestored = EnsureWorkspaceRootMarker(managedRoot);
        Directory.CreateDirectory(workspacePath);
        var markerPath = Path.Combine(workspacePath, MarkerFileName);
        var profileMarkerRestored = false;
        if (!File.Exists(markerPath))
        {
            WriteWorkspaceMarker(markerPath);
            profileMarkerRestored = true;
        }

        return rootMarkerRestored || profileMarkerRestored;
    }

    private static bool EnsureWorkspaceRootMarker(string workspaceRoot)
    {
        Directory.CreateDirectory(workspaceRoot);
        var rootMarker = Path.Combine(workspaceRoot, RootMarkerFileName);
        if (!File.Exists(rootMarker))
        {
            File.WriteAllText(rootMarker, "Managed workspace root created by Stalker Mod Launcher.");
            return true;
        }

        return false;
    }

    private static void WriteWorkspaceMarker(string markerPath)
    {
        File.WriteAllText(markerPath, "Managed by Stalker Mod Launcher. It is safe for the launcher to recreate the 'current' subfolder.");
    }

    private static string FormatElapsed(TimeSpan elapsed)
    {
        return elapsed.TotalSeconds < 1
            ? $"{elapsed.TotalMilliseconds:0} мс"
            : $"{elapsed.TotalSeconds:0.0} с";
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

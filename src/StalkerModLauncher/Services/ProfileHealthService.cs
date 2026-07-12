using StalkerModLauncher.Models;

namespace StalkerModLauncher.Services;

public sealed class ProfileHealthService
{
    private readonly GameInstallationValidator _gameValidator;
    private readonly ProfileManager _profileManager;
    private readonly ProfileDataPathResolver _dataPathResolver;
    private readonly WorkspaceManagementService _workspaceManagementService;
    private readonly ProfileLaunchPlanResolver _launchPlanResolver = new();
    private readonly OverlayManifestBuilder _overlayManifestBuilder = new();

    public ProfileHealthService(
        GameInstallationValidator gameValidator,
        ProfileManager profileManager,
        ProfileDataPathResolver dataPathResolver,
        WorkspaceManagementService workspaceManagementService)
    {
        _gameValidator = gameValidator;
        _profileManager = profileManager;
        _dataPathResolver = dataPathResolver;
        _workspaceManagementService = workspaceManagementService;
    }

    public async Task<ProfileHealthReport> AnalyzeAsync(
        ModProfile profile,
        CancellationToken cancellationToken = default)
    {
        var report = await Task.Run(() => Analyze(profile, cancellationToken), cancellationToken);
        if (profile.IsStandalone)
        {
            return report;
        }

        var workspace = await _workspaceManagementService.InspectAsync(profile, cancellationToken);
        return report with { Workspace = workspace };
    }

    private ProfileHealthReport Analyze(ModProfile profile, CancellationToken cancellationToken)
    {
        var checks = new List<ProfileHealthCheck>();
        var gamePath = profile.GameInstallPath;
        var profileFolderPath = _profileManager.GetProfileFolderPath(profile) ?? string.Empty;
        var fileLayerPlan = TryCreateLinkedFileLayerPlan(profile, profileFolderPath);
        var launchPlan = TryCreateLaunchPlan(profile, fileLayerPlan, profileFolderPath, cancellationToken);
        var overlayManifest = TryCreateOverlayManifest(profile, fileLayerPlan, profileFolderPath, cancellationToken);

        if (profile.IsStandalone)
        {
            checks.Add(new ProfileHealthCheck(
                ProfileHealthStatus.Healthy,
                "Режим профиля",
                "Автономный мод запускается непосредственно из своей папки."));
        }
        else
        {
            var validation = _gameValidator.Validate(gamePath);
            checks.Add(new ProfileHealthCheck(
                validation.IsValid ? ProfileHealthStatus.Healthy : ProfileHealthStatus.Error,
                "Базовая игра",
                $"{validation.Summary} {string.Join(" ", validation.Messages)}".Trim()));
        }

        foreach (var mod in profile.Mods.OrderBy(mod => mod.Order))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var exists = Directory.Exists(mod.SourcePath);
            checks.Add(new ProfileHealthCheck(
                exists ? ProfileHealthStatus.Healthy : mod.IsEnabled ? ProfileHealthStatus.Error : ProfileHealthStatus.Warning,
                $"Мод #{mod.Order}: {mod.Name}",
                exists
                    ? $"{(mod.IsEnabled ? "Включен" : "Выключен")}: {mod.SourcePath}"
                    : $"{(mod.IsEnabled ? "Включенный" : "Выключенный")} мод, папка не найдена: {mod.SourcePath}"));
        }

        if (profile.Mods.Count == 0)
        {
            checks.Add(new ProfileHealthCheck(
                profile.IsStandalone ? ProfileHealthStatus.Error : ProfileHealthStatus.Warning,
                "Список модов",
                profile.IsStandalone ? "Для автономного профиля требуется папка мода." : "Профиль не содержит модов."));
        }

        var executableSource = launchPlan?.Executable ?? FindExecutableSource(profile, gamePath, fileLayerPlan, cancellationToken);
        checks.Add(new ProfileHealthCheck(
            executableSource is null || !executableSource.IsAvailable
                ? ProfileHealthStatus.Error
                : executableSource.UsedRequestedRelativePath ? ProfileHealthStatus.Healthy : ProfileHealthStatus.Warning,
            "Бинарник запуска",
            executableSource is null
                ? $"Не найден: {profile.ExecutableRelativePath}"
                : FormatExecutableSource(executableSource, profile.ExecutableRelativePath)));

        var savedGamePaths = _dataPathResolver.GetSavedGameDirectories(profile);
        var savedGamesPath = savedGamePaths.FirstOrDefault(Directory.Exists)
            ?? savedGamePaths.FirstOrDefault()
            ?? string.Empty;

        if (!profile.IsStandalone)
        {
            AddProfileStorageChecks(checks, profile);
        }

        cancellationToken.ThrowIfCancellationRequested();
        var saveCount = CountFiles(savedGamesPath, "*.sav", cancellationToken);
        checks.Add(new ProfileHealthCheck(
            Directory.Exists(savedGamesPath) ? ProfileHealthStatus.Healthy : ProfileHealthStatus.Warning,
            "Сохранения",
            Directory.Exists(savedGamesPath) ? $"{saveCount} файл(ов): {savedGamesPath}" : "Папка сохранений еще не создана."));

        var logPaths = _dataPathResolver.GetLogDirectories(profile);
        var latestLog = FindLatest(logPaths, cancellationToken, ".log", ".txt");
        var latestDump = FindLatest(logPaths, cancellationToken, ".mdmp", ".dmp");
        checks.Add(new ProfileHealthCheck(
            ProfileHealthStatus.Healthy,
            "Последний лог",
            latestLog is not null ? latestLog : "Логи пока не найдены."));
        checks.Add(new ProfileHealthCheck(
            latestDump is null ? ProfileHealthStatus.Healthy : ProfileHealthStatus.Warning,
            "Crash dump",
            latestDump is not null
                ? $"Найден файл аварийного дампа: {latestDump}"
                : "Файлы аварийного дампа не найдены."));

        return new ProfileHealthReport(
            checks,
            profileFolderPath,
            savedGamesPath,
            latestLog,
            latestDump,
            LaunchPlan: launchPlan?.Plan,
            OverlayManifest: overlayManifest);
    }

    private LaunchPlanResolution? TryCreateLaunchPlan(
        ModProfile profile,
        FileLayerPlan? fileLayerPlan,
        string profileFolderPath,
        CancellationToken cancellationToken)
    {
        if (profile.IsStandalone)
        {
            return _launchPlanResolver.PreviewStandalone(profile, cancellationToken);
        }

        if (fileLayerPlan is null || string.IsNullOrWhiteSpace(profileFolderPath))
        {
            return null;
        }

        return profile.LaunchBackendKind == LaunchBackendKind.VirtualFileSystem
            ? _launchPlanResolver.PreviewVirtualFileSystem(profile, fileLayerPlan)
            : _launchPlanResolver.PreviewLinkedWorkspace(profile, fileLayerPlan, profileFolderPath);
    }

    private OverlayManifest? TryCreateOverlayManifest(
        ModProfile profile,
        FileLayerPlan? fileLayerPlan,
        string profileFolderPath,
        CancellationToken cancellationToken)
    {
        return fileLayerPlan is null || string.IsNullOrWhiteSpace(profileFolderPath)
            ? null
            : _overlayManifestBuilder.BuildLinkedWorkspace(profile, fileLayerPlan, profileFolderPath, cancellationToken: cancellationToken);
    }

    private static void AddProfileStorageChecks(List<ProfileHealthCheck> checks, ModProfile profile)
    {
        var workspacePath = profile.WorkspacePath;
        var usesVirtualFileSystem = profile.LaunchBackendKind == LaunchBackendKind.VirtualFileSystem;
        var checkTitle = usesVirtualFileSystem ? "USVFS" : "Workspace";
        if (string.IsNullOrWhiteSpace(workspacePath))
        {
            checks.Add(new ProfileHealthCheck(
                ProfileHealthStatus.Warning,
                checkTitle,
                usesVirtualFileSystem
                    ? "Профильная папка будет назначена при первом запуске. Папка current для USVFS не требуется."
                    : "Путь еще не назначен."));
            return;
        }

        if (!Directory.Exists(workspacePath))
        {
            checks.Add(new ProfileHealthCheck(
                ProfileHealthStatus.Warning,
                checkTitle,
                $"Профильная папка будет создана при запуске: {workspacePath}"));
            return;
        }

        var markerExists = File.Exists(Path.Combine(workspacePath, ".stalker-launcher-workspace"));
        checks.Add(new ProfileHealthCheck(
            markerExists ? ProfileHealthStatus.Healthy : ProfileHealthStatus.Error,
            checkTitle,
            markerExists
                ? usesVirtualFileSystem
                    ? $"Профильные данные хранятся отдельно: {workspacePath}. Папка current не используется."
                    : $"Управляемая папка: {workspacePath}"
                : $"Отсутствует защитный маркер: {workspacePath}"));

        if (usesVirtualFileSystem)
        {
            return;
        }

        var currentExists = Directory.Exists(Path.Combine(workspacePath, "current"));
        var manifestExists = File.Exists(Path.Combine(workspacePath, "build-manifest.json"));
        checks.Add(new ProfileHealthCheck(
            currentExists && manifestExists ? ProfileHealthStatus.Healthy : ProfileHealthStatus.Warning,
            "Кэш workspace",
            currentExists && manifestExists ? "Подготовленный workspace и manifest присутствуют." : "Workspace будет подготовлен или пересобран при следующем запуске."));
    }

    private static FileLayerPlan? TryCreateLinkedFileLayerPlan(ModProfile profile, string profileFolderPath)
    {
        if (profile.IsStandalone ||
            string.IsNullOrWhiteSpace(profile.GameInstallPath) ||
            string.IsNullOrWhiteSpace(profileFolderPath))
        {
            return null;
        }

        return FileLayerPlan.CreateLinkedWorkspace(profile.GameInstallPath, profile, profileFolderPath);
    }

    private LaunchExecutableResolution? FindExecutableSource(
        ModProfile profile,
        string gamePath,
        FileLayerPlan? fileLayerPlan,
        CancellationToken cancellationToken)
    {
        var roots = fileLayerPlan is null
            ? CreateExecutableRoots(profile, gamePath).ToArray()
            : FileLayerSourceResolver.CreateExecutableRoots(fileLayerPlan);
        try
        {
            FileSystemSafety.EnsureRelativePath(profile.ExecutableRelativePath, "Бинарник запуска");
        }
        catch
        {
            return null;
        }

        return _launchPlanResolver.ResolveExecutableSource(
            profile,
            roots,
            profile.ExecutableRelativePath,
            allowPinnedSource: true,
            allowDedicatedFallback: LaunchExecutableDetector.IsDedicatedExecutable(profile.ExecutableRelativePath),
            cancellationToken);
    }

    private static IEnumerable<LaunchExecutableSearchRoot> CreateExecutableRoots(ModProfile profile, string gamePath)
    {
        if (!profile.IsStandalone)
        {
            yield return new LaunchExecutableSearchRoot(gamePath, "базовая игра", 0);
        }

        foreach (var mod in profile.Mods
                     .Where(mod => mod.IsEnabled)
                     .OrderBy(mod => mod.Order))
        {
            yield return new LaunchExecutableSearchRoot(mod.SourcePath, $"мод: {mod.Name}", mod.Order);
        }
    }

    private static string FormatExecutableSource(LaunchExecutableResolution source, string requestedRelativePath)
    {
        if (!source.IsAvailable)
        {
            return $"Не найден ручной источник бинарника: {source.FullPath}{Environment.NewLine}{source.Reason}.";
        }

        if (source.IsPinned)
        {
            return $"Итоговый файл: {source.FullPath}{Environment.NewLine}Источник: {source.SourceName}. Выбран вручную; приоритет модов не заменит этот EXE.";
        }

        if (source.UsedRequestedRelativePath)
        {
            return $"Итоговый файл: {source.FullPath}{Environment.NewLine}Источник: {source.SourceName}. Нижние моды в списке имеют приоритет.";
        }

        return
            $"Выбранный путь не найден: {requestedRelativePath}{Environment.NewLine}" +
            $"Лаунчер сможет использовать: {source.FullPath}{Environment.NewLine}" +
            $"Причина выбора: {source.Reason}. Источник: {source.SourceName}.";
    }

    private static int CountFiles(string path, string pattern, CancellationToken cancellationToken)
    {
        try
        {
            return Directory.Exists(path)
                ? Directory.EnumerateFiles(path, pattern, SearchOption.TopDirectoryOnly)
                    .Count(_ =>
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        return true;
                    })
                : 0;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return 0;
        }
    }

    private static string? FindLatest(
        IEnumerable<string> paths,
        CancellationToken cancellationToken,
        params string[] extensions)
    {
        try
        {
            return paths.Where(Directory.Exists)
                .SelectMany(path => Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
                .Where(file =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    return extensions.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase);
                })
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .FirstOrDefault();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return null;
        }
    }

}

using StalkerModLauncher.Models;

namespace StalkerModLauncher.Services;

public sealed class ProfileHealthService
{
    private readonly GameInstallationValidator _gameValidator;
    private readonly ProfileManager _profileManager;
    private readonly ProfileDataPathResolver _dataPathResolver;
    private readonly WorkspaceManagementService _workspaceManagementService;

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

        var executableSource = FindExecutableSource(profile, gamePath);
        checks.Add(new ProfileHealthCheck(
            executableSource is null || !executableSource.IsAvailable
                ? ProfileHealthStatus.Error
                : executableSource.UsedRequestedRelativePath ? ProfileHealthStatus.Healthy : ProfileHealthStatus.Warning,
            "Бинарник запуска",
            executableSource is null
                ? $"Не найден: {profile.ExecutableRelativePath}"
                : FormatExecutableSource(executableSource, profile.ExecutableRelativePath)));

        var profileFolderPath = _profileManager.GetProfileFolderPath(profile) ?? string.Empty;
        var savedGamePaths = _dataPathResolver.GetSavedGameDirectories(profile);
        var savedGamesPath = savedGamePaths.FirstOrDefault(Directory.Exists)
            ?? savedGamePaths.FirstOrDefault()
            ?? string.Empty;

        if (!profile.IsStandalone)
        {
            AddWorkspaceChecks(checks, profile.WorkspacePath);
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

        return new ProfileHealthReport(checks, profileFolderPath, savedGamesPath, latestLog, latestDump);
    }

    private static void AddWorkspaceChecks(List<ProfileHealthCheck> checks, string workspacePath)
    {
        if (string.IsNullOrWhiteSpace(workspacePath))
        {
            checks.Add(new ProfileHealthCheck(ProfileHealthStatus.Warning, "Workspace", "Путь еще не назначен."));
            return;
        }

        if (!Directory.Exists(workspacePath))
        {
            checks.Add(new ProfileHealthCheck(ProfileHealthStatus.Warning, "Workspace", $"Будет создан при запуске: {workspacePath}"));
            return;
        }

        var markerExists = File.Exists(Path.Combine(workspacePath, ".stalker-launcher-workspace"));
        checks.Add(new ProfileHealthCheck(
            markerExists ? ProfileHealthStatus.Healthy : ProfileHealthStatus.Error,
            "Workspace",
            markerExists ? $"Управляемая папка: {workspacePath}" : $"Отсутствует защитный маркер: {workspacePath}"));

        var currentExists = Directory.Exists(Path.Combine(workspacePath, "current"));
        var manifestExists = File.Exists(Path.Combine(workspacePath, "build-manifest.json"));
        checks.Add(new ProfileHealthCheck(
            currentExists && manifestExists ? ProfileHealthStatus.Healthy : ProfileHealthStatus.Warning,
            "Кэш workspace",
            currentExists && manifestExists ? "Подготовленный workspace и manifest присутствуют." : "Workspace будет подготовлен или пересобран при следующем запуске."));
    }

    private static ExecutableSourceInfo? FindExecutableSource(ModProfile profile, string gamePath)
    {
        var roots = CreateExecutableRoots(profile, gamePath).ToArray();
        try
        {
            FileSystemSafety.EnsureRelativePath(profile.ExecutableRelativePath, "Бинарник запуска");
        }
        catch
        {
            return null;
        }

        if (!profile.IsStandalone && !string.IsNullOrWhiteSpace(profile.ExecutableSourcePath))
        {
            var pinnedSource = ProfileExecutableSourceResolver.FindPinnedSourceRoot(profile);
            if (pinnedSource is null)
            {
                return new ExecutableSourceInfo(
                    profile.ExecutableSourcePath,
                    profile.ExecutableRelativePath,
                    "ручной источник",
                    "папка ручного источника недоступна или мод выключен",
                    true,
                    true,
                    false);
            }

            var pinnedExecutable = FileSystemSafety.ResolvePathInside(
                pinnedSource.RootPath,
                profile.ExecutableRelativePath,
                "Бинарник запуска");
            return File.Exists(pinnedExecutable)
                ? new ExecutableSourceInfo(
                    pinnedExecutable,
                    profile.ExecutableRelativePath,
                    pinnedSource.DisplayName,
                    "выбран пользователем вручную",
                    true,
                    true)
                : new ExecutableSourceInfo(
                    pinnedExecutable,
                    profile.ExecutableRelativePath,
                    pinnedSource.DisplayName,
                    "ручной источник найден, но файл отсутствует",
                    true,
                    true,
                    false);
        }

        var exact = roots
            .Where(root => Directory.Exists(root.RootPath))
            .Select(root => new
            {
                FullPath = Path.Combine(root.RootPath, profile.ExecutableRelativePath),
                root.DisplayName,
                root.Order
            })
            .Where(candidate => File.Exists(candidate.FullPath))
            .OrderByDescending(candidate => candidate.Order)
            .FirstOrDefault();
        if (exact is not null)
        {
            return new ExecutableSourceInfo(
                exact.FullPath,
                profile.ExecutableRelativePath,
                exact.DisplayName,
                "найден выбранный путь",
                true,
                false);
        }

        var detected = LaunchExecutableDetector.DetectBest(
            roots,
            requestedRelativePath: profile.ExecutableRelativePath,
            allowDedicated: LaunchExecutableDetector.IsDedicatedExecutable(profile.ExecutableRelativePath));
        if (detected is null || detected.Score > 50 && detected.CandidateCount != 1)
        {
            return null;
        }

        return new ExecutableSourceInfo(
            detected.FullPath,
            detected.RelativePath,
            detected.SourceName,
            detected.Reason,
            false,
            false);
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

    private static string FormatExecutableSource(ExecutableSourceInfo source, string requestedRelativePath)
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

    private sealed record ExecutableSourceInfo(
        string FullPath,
        string RelativePath,
        string SourceName,
        string Reason,
        bool UsedRequestedRelativePath,
        bool IsPinned,
        bool IsAvailable = true);
}

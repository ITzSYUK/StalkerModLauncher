using StalkerModLauncher.Models;

namespace StalkerModLauncher.Services;

public sealed class ProfileHealthService
{
    private readonly GameInstallationValidator _gameValidator;
    private readonly ProfileManager _profileManager;
    private readonly ProfileDataPathResolver _dataPathResolver;

    public ProfileHealthService(
        GameInstallationValidator gameValidator,
        ProfileManager profileManager,
        ProfileDataPathResolver dataPathResolver)
    {
        _gameValidator = gameValidator;
        _profileManager = profileManager;
        _dataPathResolver = dataPathResolver;
    }

    public Task<ProfileHealthReport> AnalyzeAsync(
        ModProfile profile,
        string defaultGamePath,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(() => Analyze(profile, defaultGamePath, cancellationToken), cancellationToken);
    }

    private ProfileHealthReport Analyze(ModProfile profile, string defaultGamePath, CancellationToken cancellationToken)
    {
        var checks = new List<ProfileHealthCheck>();
        var gamePath = string.IsNullOrWhiteSpace(profile.GameInstallPath) ? defaultGamePath : profile.GameInstallPath;

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
            executableSource is null ? ProfileHealthStatus.Error : ProfileHealthStatus.Healthy,
            "Бинарник запуска",
            executableSource is null
                ? $"Не найден: {profile.ExecutableRelativePath}"
                : executableSource));

        var profileFolderPath = _profileManager.GetProfileFolderPath(profile, defaultGamePath) ?? string.Empty;
        var savedGamePaths = _dataPathResolver.GetSavedGameDirectories(profile);
        var savedGamesPath = savedGamePaths.FirstOrDefault(Directory.Exists)
            ?? savedGamePaths.FirstOrDefault()
            ?? string.Empty;

        if (!profile.IsStandalone)
        {
            AddWorkspaceChecks(checks, profile.WorkspacePath);
        }

        var saveCount = CountFiles(savedGamesPath, "*.sav");
        checks.Add(new ProfileHealthCheck(
            Directory.Exists(savedGamesPath) ? ProfileHealthStatus.Healthy : ProfileHealthStatus.Warning,
            "Сохранения",
            Directory.Exists(savedGamesPath) ? $"{saveCount} файл(ов): {savedGamesPath}" : "Папка сохранений еще не создана."));

        var logPaths = _dataPathResolver.GetLogDirectories(profile);
        var latestLog = FindLatest(logPaths, ".log", ".txt");
        var latestDump = FindLatest(logPaths, ".mdmp", ".dmp");
        checks.Add(new ProfileHealthCheck(
            latestDump is null ? ProfileHealthStatus.Healthy : ProfileHealthStatus.Warning,
            "Диагностика игры",
            latestDump is not null
                ? $"Найден crash dump: {latestDump}"
                : latestLog is not null ? $"Последний лог: {latestLog}" : "Логи и crash dump пока не найдены."));

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

    private static string? FindExecutableSource(ModProfile profile, string gamePath)
    {
        var roots = new List<string>();
        if (profile.IsStandalone)
        {
            roots.AddRange(profile.Mods.Where(mod => mod.IsEnabled).Select(mod => mod.SourcePath));
        }
        else
        {
            roots.Add(gamePath);
            roots.AddRange(profile.Mods.Where(mod => mod.IsEnabled).Select(mod => mod.SourcePath));
            if (!string.IsNullOrWhiteSpace(profile.WorkspacePath))
            {
                roots.Add(Path.Combine(profile.WorkspacePath, "current"));
            }
        }

        return roots.Where(Directory.Exists)
            .Select(root => Path.Combine(root, profile.ExecutableRelativePath))
            .FirstOrDefault(File.Exists);
    }

    private static int CountFiles(string path, string pattern)
    {
        try
        {
            return Directory.Exists(path) ? Directory.EnumerateFiles(path, pattern, SearchOption.TopDirectoryOnly).Count() : 0;
        }
        catch
        {
            return 0;
        }
    }

    private static string? FindLatest(IEnumerable<string> paths, params string[] extensions)
    {
        try
        {
            return paths.Where(Directory.Exists)
                .SelectMany(path => Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
                    .Where(file => extensions.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase))
                    .OrderByDescending(File.GetLastWriteTimeUtc)
                    .FirstOrDefault();
        }
        catch
        {
            return null;
        }
    }
}

using StalkerModLauncher.Models;

namespace StalkerModLauncher.Services;

public sealed class LaunchPreflightService
{
    private const long LowDiskSpaceBytes = 512L * 1024 * 1024;
    private readonly GameInstallationValidator _gameValidator;
    private readonly ProfileManager _profileManager;

    public LaunchPreflightService(GameInstallationValidator gameValidator, ProfileManager profileManager)
    {
        _gameValidator = gameValidator;
        _profileManager = profileManager;
    }

    public Task<LaunchPreflightReport> AnalyzeAsync(ModProfile profile, CancellationToken cancellationToken = default)
    {
        return Task.Run(() => Analyze(profile, cancellationToken), cancellationToken);
    }

    private LaunchPreflightReport Analyze(ModProfile profile, CancellationToken cancellationToken)
    {
        var checks = new List<ProfileHealthCheck>();
        if (!profile.IsEnabled)
        {
            checks.Add(Error("Профиль", "Профиль выключен."));
        }

        if (!profile.IsStandalone)
        {
            var game = _gameValidator.Validate(profile.GameInstallPath);
            checks.Add(new ProfileHealthCheck(
                game.IsValid ? ProfileHealthStatus.Healthy : ProfileHealthStatus.Error,
                "Базовая игра",
                game.IsValid ? profile.GameInstallPath : $"{game.Summary} {string.Join(" ", game.Messages)}".Trim()));
        }

        var enabledMods = profile.Mods.Where(mod => mod.IsEnabled).OrderBy(mod => mod.Order).ToArray();
        if (profile.IsStandalone && enabledMods.Length != 1)
        {
            checks.Add(Error("Автономный мод", "Автономный профиль должен содержать ровно одну включённую папку мода."));
        }

        foreach (var mod in enabledMods)
        {
            cancellationToken.ThrowIfCancellationRequested();
            checks.Add(new ProfileHealthCheck(
                Directory.Exists(mod.SourcePath) ? ProfileHealthStatus.Healthy : ProfileHealthStatus.Error,
                $"Источник: {mod.Name}",
                Directory.Exists(mod.SourcePath) ? mod.SourcePath : $"Папка не найдена: {mod.SourcePath}"));
        }

        try
        {
            FileSystemSafety.EnsureRelativePath(profile.ExecutableRelativePath, "Бинарник запуска");
            var executableSource = FindFinalSource(profile, profile.ExecutableRelativePath);
            checks.Add(new ProfileHealthCheck(
                executableSource is null ? ProfileHealthStatus.Error : ProfileHealthStatus.Healthy,
                "Итоговый бинарник",
                executableSource ?? $"Не найден файл {profile.ExecutableRelativePath} ни в игре, ни во включённых модах."));

            if (executableSource is not null)
            {
                AddCompanionDllCheck(checks, profile, executableSource);
            }
        }
        catch (Exception ex)
        {
            checks.Add(Error("Бинарник запуска", ex.Message));
        }

        var fsgameSource = FindFinalSource(profile, "fsgame.ltx");
        checks.Add(new ProfileHealthCheck(
            fsgameSource is null ? ProfileHealthStatus.Warning : ProfileHealthStatus.Healthy,
            "fsgame.ltx",
            fsgameSource ?? "Файл не найден. Некоторые движки запускаются без него, но сохранения профиля могут не изолироваться."));

        if (!profile.IsStandalone)
        {
            AddWorkspaceChecks(checks, profile);
            AddLinkSupportCheck(checks, profile, enabledMods, cancellationToken);
        }

        return new LaunchPreflightReport(checks);
    }

    private void AddWorkspaceChecks(List<ProfileHealthCheck> checks, ModProfile profile)
    {
        var workspace = _profileManager.GetProfileFolderPath(profile);
        if (string.IsNullOrWhiteSpace(workspace))
        {
            checks.Add(Error("Workspace", "Не удалось определить путь рабочего пространства."));
            return;
        }

        try
        {
            var root = Path.GetPathRoot(Path.GetFullPath(workspace));
            var drive = root is null ? null : new DriveInfo(root);
            checks.Add(new ProfileHealthCheck(
                drive is { AvailableFreeSpace: < LowDiskSpaceBytes } ? ProfileHealthStatus.Warning : ProfileHealthStatus.Healthy,
                "Свободное место",
                drive is null ? "Не удалось определить диск workspace." : $"{WorkspaceStatus.FormatSize(drive.AvailableFreeSpace)} доступно на {drive.Name}"));
        }
        catch (Exception ex)
        {
            checks.Add(new ProfileHealthCheck(ProfileHealthStatus.Warning, "Свободное место", ex.Message));
        }
    }

    private void AddLinkSupportCheck(
        List<ProfileHealthCheck> checks,
        ModProfile profile,
        IReadOnlyList<ModEntry> enabledMods,
        CancellationToken cancellationToken)
    {
        var workspace = _profileManager.GetProfileFolderPath(profile);
        if (string.IsNullOrWhiteSpace(workspace))
        {
            return;
        }

        try
        {
            var workspaceVolume = Path.GetPathRoot(Path.GetFullPath(workspace));
            var source = enabledMods
                .Select(mod => mod.SourcePath)
                .Where(Directory.Exists)
                .Where(path => !string.Equals(Path.GetPathRoot(Path.GetFullPath(path)), workspaceVolume, StringComparison.OrdinalIgnoreCase))
                .SelectMany(path => Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories).Take(1))
                .FirstOrDefault();
            if (source is null)
            {
                return;
            }

            cancellationToken.ThrowIfCancellationRequested();
            Directory.CreateDirectory(workspace);
            var testLink = Path.Combine(workspace, $".preflight-link-{Guid.NewGuid():N}");
            try
            {
                File.CreateSymbolicLink(testLink, source);
                checks.Add(new ProfileHealthCheck(
                    ProfileHealthStatus.Healthy,
                    "Ссылки между дисками",
                    $"Windows разрешает подключать файлы с диска {Path.GetPathRoot(source)} без копирования."));
            }
            finally
            {
                File.Delete(testLink);
            }
        }
        catch (Exception ex)
        {
            checks.Add(Error(
                "Ссылки между дисками",
                "Windows не разрешила создать символическую ссылку. Включите режим разработчика, запустите лаунчер от имени администратора или разместите мод на диске workspace. " + ex.Message));
        }
    }

    private static void AddCompanionDllCheck(List<ProfileHealthCheck> checks, ModProfile profile, string executableSource)
    {
        var executableName = Path.GetFileName(executableSource);
        if (!executableName.Contains("xr", StringComparison.OrdinalIgnoreCase) &&
            !executableName.Contains("ogsr", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var relativeDirectory = Path.GetDirectoryName(profile.ExecutableRelativePath) ?? string.Empty;
        var hasEngineDll = new[] { "xrCore.dll", "xrGame.dll", "xrEngine.dll" }
            .Any(name => FindFinalSource(profile, Path.Combine(relativeDirectory, name)) is not null);
        checks.Add(new ProfileHealthCheck(
            hasEngineDll ? ProfileHealthStatus.Healthy : ProfileHealthStatus.Warning,
            "Файлы движка",
            hasEngineDll
                ? "Рядом с бинарником найдены DLL движка."
                : "Рядом с выбранным бинарником не найдены типичные DLL движка. Это допустимо не для всех сборок."));
    }

    internal static string? FindFinalSource(ModProfile profile, string relativePath)
    {
        var roots = new List<string>();
        if (!profile.IsStandalone)
        {
            roots.Add(profile.GameInstallPath);
        }

        roots.AddRange(profile.Mods
            .Where(mod => mod.IsEnabled)
            .OrderBy(mod => mod.Order)
            .Select(mod => mod.SourcePath));

        return roots
            .Where(Directory.Exists)
            .Select(root => Path.Combine(root, relativePath))
            .LastOrDefault(File.Exists);
    }

    private static ProfileHealthCheck Error(string title, string details) =>
        new(ProfileHealthStatus.Error, title, details);
}

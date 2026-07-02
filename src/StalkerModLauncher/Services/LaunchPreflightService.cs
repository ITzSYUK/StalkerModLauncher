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
            var exists = Directory.Exists(mod.SourcePath);
            checks.Add(new ProfileHealthCheck(
                exists ? ProfileHealthStatus.Healthy : ProfileHealthStatus.Error,
                $"Источник: {mod.Name}",
                exists ? mod.SourcePath : $"Папка не найдена: {mod.SourcePath}"));

            if (exists && !HasAnyFile(mod.SourcePath, cancellationToken))
            {
                checks.Add(new ProfileHealthCheck(
                    ProfileHealthStatus.Warning,
                    $"Мод пуст: {mod.Name}",
                    "В папке не найдено файлов. Возможно, выбрана внешняя или неправильная папка мода."));
            }
        }

        try
        {
            FileSystemSafety.EnsureRelativePath(profile.ExecutableRelativePath, "Бинарник запуска");
            var executableSource = FindFinalExecutableSource(profile, profile.ExecutableRelativePath);
            checks.Add(new ProfileHealthCheck(
                executableSource is null || !executableSource.IsAvailable
                    ? ProfileHealthStatus.Error
                    : executableSource.UsedRequestedRelativePath ? ProfileHealthStatus.Healthy : ProfileHealthStatus.Warning,
                "Итоговый бинарник",
                executableSource is null
                    ? $"Не найден файл {profile.ExecutableRelativePath} ни в игре, ни во включённых модах."
                    : FormatExecutableSource(executableSource, profile.ExecutableRelativePath)));

            if (executableSource is { IsAvailable: true })
            {
                AddCompanionDllCheck(checks, profile, executableSource.FullPath, executableSource.RelativePath);
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

    private static void AddCompanionDllCheck(
        List<ProfileHealthCheck> checks,
        ModProfile profile,
        string executableSource,
        string executableRelativePath)
    {
        var executableName = Path.GetFileName(executableSource);
        if (!executableName.Contains("xr", StringComparison.OrdinalIgnoreCase) &&
            !executableName.Contains("ogsr", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var relativeDirectory = Path.GetDirectoryName(executableRelativePath) ?? string.Empty;
        var hasEngineDll = new[] { "xrCore.dll", "xrGame.dll", "xrEngine.dll" }
            .Any(name => FindFinalSource(profile, Path.Combine(relativeDirectory, name)) is not null);
        checks.Add(new ProfileHealthCheck(
            hasEngineDll ? ProfileHealthStatus.Healthy : ProfileHealthStatus.Warning,
            "Файлы движка",
            hasEngineDll
                ? "Рядом с бинарником найдены DLL движка."
                : "Рядом с выбранным бинарником не найдены типичные DLL движка. Это допустимо не для всех сборок."));
    }

    private static bool HasAnyFile(string root, CancellationToken cancellationToken)
    {
        try
        {
            foreach (var _ in Directory.EnumerateFiles(root, "*", SafeEnumerationOptions).Take(1))
            {
                cancellationToken.ThrowIfCancellationRequested();
                return true;
            }
        }
        catch (UnauthorizedAccessException)
        {
            return true;
        }
        catch (IOException)
        {
            return true;
        }

        return false;
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

    private static ExecutableSourceInfo? FindFinalExecutableSource(ModProfile profile, string requestedRelativePath)
    {
        var roots = CreateExecutableRoots(profile).ToArray();
        if (!profile.IsStandalone && !string.IsNullOrWhiteSpace(profile.ExecutableSourcePath))
        {
            var pinnedSource = ProfileExecutableSourceResolver.FindPinnedSourceRoot(profile);
            if (pinnedSource is null)
            {
                return new ExecutableSourceInfo(
                    profile.ExecutableSourcePath,
                    requestedRelativePath,
                    "ручной источник",
                    "папка ручного источника недоступна или мод выключен",
                    true,
                    true,
                    false);
            }

            var pinnedExecutable = FileSystemSafety.ResolvePathInside(
                pinnedSource.RootPath,
                requestedRelativePath,
                "Бинарник запуска");
            return File.Exists(pinnedExecutable)
                ? new ExecutableSourceInfo(
                    pinnedExecutable,
                    requestedRelativePath,
                    pinnedSource.DisplayName,
                    "выбран пользователем вручную",
                    true,
                    true)
                : new ExecutableSourceInfo(
                    pinnedExecutable,
                    requestedRelativePath,
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
                FullPath = Path.Combine(root.RootPath, requestedRelativePath),
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
                requestedRelativePath,
                exact.DisplayName,
                "найден выбранный путь",
                true,
                false);
        }

        var detected = LaunchExecutableDetector.DetectBest(
            roots,
            requestedRelativePath,
            allowDedicated: LaunchExecutableDetector.IsDedicatedExecutable(requestedRelativePath));
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

    private static IEnumerable<LaunchExecutableSearchRoot> CreateExecutableRoots(ModProfile profile)
    {
        if (!profile.IsStandalone)
        {
            yield return new LaunchExecutableSearchRoot(profile.GameInstallPath, "базовая игра", 0);
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
            return $"Не найден ручной источник бинарника: {source.FullPath}. {source.Reason}.";
        }

        if (source.IsPinned)
        {
            return $"Итоговый файл: {source.FullPath}. Источник: {source.SourceName}. Выбран вручную; приоритет модов не заменит этот EXE.";
        }

        if (source.UsedRequestedRelativePath)
        {
            return $"Итоговый файл: {source.FullPath}. Источник: {source.SourceName}.";
        }

        return
            $"Выбранный путь не найден: {requestedRelativePath}. " +
            $"Будет использован: {source.FullPath}. " +
            $"Причина: {source.Reason}. Источник: {source.SourceName}.";
    }

    private static ProfileHealthCheck Error(string title, string details) =>
        new(ProfileHealthStatus.Error, title, details);

    private static EnumerationOptions SafeEnumerationOptions { get; } = new()
    {
        RecurseSubdirectories = true,
        IgnoreInaccessible = true,
        AttributesToSkip = FileAttributes.ReparsePoint
    };

    private sealed record ExecutableSourceInfo(
        string FullPath,
        string RelativePath,
        string SourceName,
        string Reason,
        bool UsedRequestedRelativePath,
        bool IsPinned,
        bool IsAvailable = true);
}

using StalkerModLauncher.Models;

namespace StalkerModLauncher.Services;

public sealed class LaunchPreflightService
{
    private const long LowDiskSpaceBytes = 512L * 1024 * 1024;
    private readonly GameInstallationValidator _gameValidator;
    private readonly ProfileManager _profileManager;
    private readonly ProfileLaunchPlanResolver _launchPlanResolver = new();
    private readonly OverlayManifestBuilder _overlayManifestBuilder = new();

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
        var fileLayerPlan = TryCreateLinkedFileLayerPlan(profile);
        var launchPlan = TryCreateLaunchPlan(profile, fileLayerPlan, cancellationToken);
        var overlayManifest = TryCreateOverlayManifest(profile, fileLayerPlan, cancellationToken);
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
            var executableSource = launchPlan?.Executable ??
                                   FindFinalExecutableSource(profile, profile.ExecutableRelativePath, fileLayerPlan, cancellationToken);
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
                AddCompanionDllCheck(checks, profile, fileLayerPlan, executableSource.FullPath, executableSource.RelativePath);
            }
        }
        catch (Exception ex)
        {
            checks.Add(Error("Бинарник запуска", ex.Message));
        }

        var fsgameSource = FindFinalSource(profile, "fsgame.ltx", fileLayerPlan);
        checks.Add(new ProfileHealthCheck(
            fsgameSource is null ? ProfileHealthStatus.Warning : ProfileHealthStatus.Healthy,
            "fsgame.ltx",
            fsgameSource ?? "Файл не найден. Некоторые движки запускаются без него, но сохранения профиля могут не изолироваться."));

        if (!profile.IsStandalone)
        {
            AddWorkspaceChecks(checks, profile);
            AddLinkSupportCheck(checks, profile, fileLayerPlan, enabledMods, cancellationToken);
        }

        return new LaunchPreflightReport(checks, launchPlan?.Plan, overlayManifest);
    }

    private LaunchPlanResolution? TryCreateLaunchPlan(
        ModProfile profile,
        FileLayerPlan? fileLayerPlan,
        CancellationToken cancellationToken)
    {
        if (profile.IsStandalone)
        {
            return _launchPlanResolver.PreviewStandalone(profile, cancellationToken);
        }

        if (fileLayerPlan is null)
        {
            return null;
        }

        var workspace = _profileManager.GetProfileFolderPath(profile);
        return string.IsNullOrWhiteSpace(workspace)
            ? null
            : _launchPlanResolver.PreviewLinkedWorkspace(profile, fileLayerPlan, workspace);
    }

    private OverlayManifest? TryCreateOverlayManifest(
        ModProfile profile,
        FileLayerPlan? fileLayerPlan,
        CancellationToken cancellationToken)
    {
        if (fileLayerPlan is null)
        {
            return null;
        }

        var workspace = _profileManager.GetProfileFolderPath(profile);
        return string.IsNullOrWhiteSpace(workspace)
            ? null
            : _overlayManifestBuilder.BuildLinkedWorkspace(profile, fileLayerPlan, workspace, cancellationToken: cancellationToken);
    }

    private FileLayerPlan? TryCreateLinkedFileLayerPlan(ModProfile profile)
    {
        if (profile.IsStandalone || string.IsNullOrWhiteSpace(profile.GameInstallPath))
        {
            return null;
        }

        var workspace = _profileManager.GetProfileFolderPath(profile);
        if (string.IsNullOrWhiteSpace(workspace))
        {
            return null;
        }

        return FileLayerPlan.CreateLinkedWorkspace(profile.GameInstallPath, profile, workspace);
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
        FileLayerPlan? fileLayerPlan,
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
            var sourcePaths = fileLayerPlan is null
                ? enabledMods.Select(mod => mod.SourcePath)
                : fileLayerPlan.Mods.Select(layer => layer.RootPath);
            var source = sourcePaths
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
        FileLayerPlan? fileLayerPlan,
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
            .Any(name => FindFinalSource(profile, Path.Combine(relativeDirectory, name), fileLayerPlan) is not null);
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

    internal static string? FindFinalSource(ModProfile profile, string relativePath, FileLayerPlan? fileLayerPlan = null)
    {
        if (fileLayerPlan is not null)
        {
            return FileLayerSourceResolver.FindFinalSource(fileLayerPlan, relativePath)?.FullPath;
        }

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

    private LaunchExecutableResolution? FindFinalExecutableSource(
        ModProfile profile,
        string requestedRelativePath,
        FileLayerPlan? fileLayerPlan,
        CancellationToken cancellationToken)
    {
        var roots = fileLayerPlan is null
            ? CreateExecutableRoots(profile).ToArray()
            : FileLayerSourceResolver.CreateExecutableRoots(fileLayerPlan);
        return _launchPlanResolver.ResolveExecutableSource(
            profile,
            roots,
            requestedRelativePath,
            allowPinnedSource: true,
            allowDedicatedFallback: LaunchExecutableDetector.IsDedicatedExecutable(requestedRelativePath),
            cancellationToken);
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

    private static string FormatExecutableSource(LaunchExecutableResolution source, string requestedRelativePath)
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

}

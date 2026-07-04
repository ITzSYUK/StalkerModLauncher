using StalkerModLauncher.Models;
using StalkerModLauncher.Services;
using Xunit;

namespace StalkerModLauncher.Tests;

public sealed class LaunchPreflightServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "StalkerModLauncherTests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task AnalyzeAsync_FindsFinalExecutableFromLastEnabledMod()
    {
        var paths = new AppPaths(_root, Path.Combine(_root, "workspaces"), false);
        var builder = new WorkspaceBuilder(paths);
        var service = new LaunchPreflightService(
            new GameInstallationValidator(),
            new ProfileManager(paths, builder));
        var game = CreateFile("game/fsgame.ltx");
        CreateFile("game/bin/xr_3da.exe");
        var patchExecutable = CreateFile("patch/bin/xr_3da.exe");
        var profile = new ModProfile
        {
            GameInstallPath = Path.GetDirectoryName(game)!,
            ExecutableRelativePath = @"bin\xr_3da.exe"
        };
        profile.Mods.Add(new ModEntry { Name = "Patch", SourcePath = Path.Combine(_root, "patch"), Order = 1 });

        var report = await service.AnalyzeAsync(profile);

        Assert.True(report.CanLaunch);
        Assert.Contains(
            report.Checks,
            check => check.Title == "Итоговый бинарник" &&
                     check.Status == ProfileHealthStatus.Healthy &&
                     check.Details.Contains(patchExecutable));
    }

    [Fact]
    public async Task AnalyzeAsync_UsesPinnedExecutableSource()
    {
        var paths = new AppPaths(_root, Path.Combine(_root, "workspaces"), false);
        var builder = new WorkspaceBuilder(paths);
        var service = new LaunchPreflightService(
            new GameInstallationValidator(),
            new ProfileManager(paths, builder));
        var game = CreateFile("game-pinned/fsgame.ltx");
        CreateFile("game-pinned/bin/xr_3da.exe");
        var mainExecutable = CreateFile("main/bin_x64/xrEngine.exe");
        CreateFile("patch/bin_x64/xrEngine.exe");
        var profile = new ModProfile
        {
            GameInstallPath = Path.GetDirectoryName(game)!,
            ExecutableRelativePath = @"bin_x64\xrEngine.exe",
            ExecutableSourcePath = Path.Combine(_root, "main")
        };
        profile.Mods.Add(new ModEntry { Name = "Main", SourcePath = Path.Combine(_root, "main"), Order = 1 });
        profile.Mods.Add(new ModEntry { Name = "Patch", SourcePath = Path.Combine(_root, "patch"), Order = 2 });

        var report = await service.AnalyzeAsync(profile);

        Assert.True(report.CanLaunch);
        Assert.Contains(
            report.Checks,
            check => check.Title == "Итоговый бинарник" &&
                     check.Status == ProfileHealthStatus.Healthy &&
                     check.Details.Contains(mainExecutable) &&
                     check.Details.Contains("Выбран вручную"));
    }

    [Fact]
    public async Task AnalyzeAsync_ReportsFsgameFromHighestPriorityLayer()
    {
        var paths = new AppPaths(_root, Path.Combine(_root, "workspaces"), false);
        var builder = new WorkspaceBuilder(paths);
        var service = new LaunchPreflightService(
            new GameInstallationValidator(),
            new ProfileManager(paths, builder));
        var game = Path.Combine(_root, "game-layered");
        CreateFile("game-layered/fsgame.ltx");
        CreateFile("game-layered/bin/xr_3da.exe");
        CreateFile("main/fsgame.ltx");
        var patchFsgame = CreateFile("patch/fsgame.ltx");
        var profile = new ModProfile
        {
            GameInstallPath = game,
            ExecutableRelativePath = @"bin\xr_3da.exe"
        };
        profile.Mods.Add(new ModEntry { Name = "Patch", SourcePath = Path.Combine(_root, "patch"), Order = 2 });
        profile.Mods.Add(new ModEntry { Name = "Main", SourcePath = Path.Combine(_root, "main"), Order = 1 });

        var report = await service.AnalyzeAsync(profile);

        Assert.Contains(
            report.Checks,
            check => check.Title == "fsgame.ltx" &&
                     check.Status == ProfileHealthStatus.Healthy &&
                     check.Details == patchFsgame);
    }

    [Fact]
    public async Task AnalyzeAsync_WarnsAndFallsBackWhenRequestedExecutableIsMissing()
    {
        var paths = new AppPaths(_root, Path.Combine(_root, "workspaces"), false);
        var builder = new WorkspaceBuilder(paths);
        var service = new LaunchPreflightService(
            new GameInstallationValidator(),
            new ProfileManager(paths, builder));
        var game = CreateFile("game/fsgame.ltx");
        CreateFile("game/bin/xr_3da.exe");
        var profile = new ModProfile
        {
            GameInstallPath = Path.GetDirectoryName(game)!,
            ExecutableRelativePath = @"bin\missing.exe"
        };

        var report = await service.AnalyzeAsync(profile);

        Assert.True(report.CanLaunch);
        Assert.Contains(
            report.Checks,
            check => check.Title == "Итоговый бинарник" &&
                     check.Status == ProfileHealthStatus.Warning &&
                     check.Details.Contains(@"bin\missing.exe") &&
                     check.Details.Contains(@"bin\xr_3da.exe"));
    }

    [Fact]
    public async Task AnalyzeAsync_BlocksWhenNoExecutableCanBeDetected()
    {
        var paths = new AppPaths(_root, Path.Combine(_root, "workspaces"), false);
        var builder = new WorkspaceBuilder(paths);
        var service = new LaunchPreflightService(
            new GameInstallationValidator(),
            new ProfileManager(paths, builder));
        var game = CreateFile("broken-game/fsgame.ltx");
        var profile = new ModProfile
        {
            GameInstallPath = Path.GetDirectoryName(game)!,
            ExecutableRelativePath = @"bin\missing.exe"
        };

        var report = await service.AnalyzeAsync(profile);

        Assert.False(report.CanLaunch);
        Assert.Contains(
            report.Checks,
            check => check.Title == "Итоговый бинарник" && check.Status == ProfileHealthStatus.Error);
    }

    [Fact]
    public async Task AnalyzeAsync_WarnsWhenEnabledModFolderIsEmpty()
    {
        var paths = new AppPaths(_root, Path.Combine(_root, "workspaces"), false);
        var builder = new WorkspaceBuilder(paths);
        var service = new LaunchPreflightService(
            new GameInstallationValidator(),
            new ProfileManager(paths, builder));
        var game = CreateFile("game-empty-mod/fsgame.ltx");
        CreateFile("game-empty-mod/bin/xr_3da.exe");
        var emptyMod = Path.Combine(_root, "empty-mod");
        Directory.CreateDirectory(emptyMod);
        var profile = new ModProfile
        {
            GameInstallPath = Path.GetDirectoryName(game)!,
            ExecutableRelativePath = @"bin\xr_3da.exe"
        };
        profile.Mods.Add(new ModEntry { Name = "Empty", SourcePath = emptyMod, Order = 1 });

        var report = await service.AnalyzeAsync(profile);

        Assert.True(report.CanLaunch);
        Assert.Contains(
            report.Checks,
            check => check.Title == "Мод пуст: Empty" && check.Status == ProfileHealthStatus.Warning);
    }

    private string CreateFile(string relativePath)
    {
        var path = Path.Combine(_root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "test");
        return path;
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, true);
        }
    }
}

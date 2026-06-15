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
        Assert.Contains(report.Checks, check => check.Title == "Итоговый бинарник" && check.Details == patchExecutable);
    }

    [Fact]
    public async Task AnalyzeAsync_BlocksMissingExecutable()
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

        Assert.False(report.CanLaunch);
        Assert.Contains(report.Checks, check => check.Title == "Итоговый бинарник" && check.Status == ProfileHealthStatus.Error);
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

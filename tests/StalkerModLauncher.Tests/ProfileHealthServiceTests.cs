using StalkerModLauncher.Models;
using StalkerModLauncher.Services;
using Xunit;

namespace StalkerModLauncher.Tests;

public sealed class ProfileHealthServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "StalkerModLauncherTests",
        Guid.NewGuid().ToString("N"));
    private readonly ProfileHealthService _service;

    public ProfileHealthServiceTests()
    {
        var paths = new AppPaths(_root, Path.Combine(_root, "workspaces"), false);
        var manager = new ProfileManager(paths, new FakeWorkspaceManager());
        _service = new ProfileHealthService(new GameInstallationValidator(), manager);
    }

    [Fact]
    public async Task AnalyzeAsync_ReportsReadyOverlayProfile()
    {
        var game = CreateGame();
        var mod = CreateFile("mod/gamedata/test.ltx");
        var workspace = Path.Combine(_root, "workspace");
        CreateFileAtPath(Path.Combine(workspace, ".stalker-launcher-workspace"));
        CreateFileAtPath(Path.Combine(workspace, "build-manifest.json"));
        CreateFileAtPath(Path.Combine(workspace, "current", "bin", "xr_3da.exe"));
        CreateFileAtPath(Path.Combine(workspace, "userdata", "savedgames", "test.sav"));
        var profile = new ModProfile { GameInstallPath = game, WorkspacePath = workspace };
        profile.Mods.Add(new ModEntry { Name = "Mod", SourcePath = Path.GetDirectoryName(Path.GetDirectoryName(mod))!, Order = 1 });

        var report = await _service.AnalyzeAsync(profile, string.Empty);

        Assert.True(report.IsReady);
        Assert.Equal(0, report.ErrorCount);
        Assert.Contains(report.Checks, check => check.Title == "Сохранения" && check.Details.StartsWith("1 файл"));
    }

    [Fact]
    public async Task AnalyzeAsync_ReportsMissingModAndExecutable()
    {
        var profile = new ModProfile
        {
            GameInstallPath = CreateGame(),
            ExecutableRelativePath = @"bin\missing.exe"
        };
        profile.Mods.Add(new ModEntry { Name = "Missing", SourcePath = Path.Combine(_root, "missing"), Order = 1 });

        var report = await _service.AnalyzeAsync(profile, string.Empty);

        Assert.False(report.IsReady);
        Assert.Contains(report.Checks, check => check.Title.Contains("Missing") && check.Status == ProfileHealthStatus.Error);
        Assert.Contains(report.Checks, check => check.Title == "Бинарник запуска" && check.Status == ProfileHealthStatus.Error);
    }

    [Fact]
    public async Task AnalyzeAsync_TreatsMissingDisabledModAsWarning()
    {
        var profile = new ModProfile { GameInstallPath = CreateGame() };
        profile.Mods.Add(new ModEntry
        {
            Name = "Disabled",
            SourcePath = Path.Combine(_root, "missing"),
            IsEnabled = false,
            Order = 1
        });

        var report = await _service.AnalyzeAsync(profile, string.Empty);

        Assert.True(report.IsReady);
        Assert.Contains(report.Checks, check => check.Title.Contains("Disabled") && check.Status == ProfileHealthStatus.Warning);
    }

    [Fact]
    public async Task AnalyzeAsync_FindsLatestLogAndCrashDump()
    {
        var modRoot = Path.Combine(_root, "standalone");
        CreateFileAtPath(Path.Combine(modRoot, "bin_x64", "xrEngine.exe"));
        var workspace = Path.Combine(_root, "workspace");
        var log = CreateFileAtPath(Path.Combine(workspace, "userdata", "logs", "xray.log"));
        var dump = CreateFileAtPath(Path.Combine(workspace, "userdata", "logs", "xray.mdmp"));
        var profile = new ModProfile
        {
            IsStandalone = true,
            ExecutableRelativePath = @"bin_x64\xrEngine.exe",
            WorkspacePath = workspace
        };
        profile.Mods.Add(new ModEntry { Name = "Standalone", SourcePath = modRoot, Order = 1 });

        var report = await _service.AnalyzeAsync(profile, string.Empty);

        Assert.Equal(log, report.LatestLogPath);
        Assert.Equal(dump, report.LatestCrashDumpPath);
        Assert.Equal(modRoot, report.ProfileFolderPath);
        Assert.Equal(2, report.WarningCount);
        Assert.Contains("crash dump", report.ToText("Standalone"));
    }

    [Fact]
    public async Task AnalyzeAsync_UsesWorkspaceAsOverlayProfileFolder()
    {
        var workspace = Path.Combine(_root, "workspace");
        Directory.CreateDirectory(workspace);
        var profile = new ModProfile { GameInstallPath = CreateGame(), WorkspacePath = workspace };

        var report = await _service.AnalyzeAsync(profile, string.Empty);

        Assert.Equal(workspace, report.ProfileFolderPath);
    }

    private string CreateGame()
    {
        var path = Path.Combine(_root, "game");
        CreateFileAtPath(Path.Combine(path, "fsgame.ltx"));
        CreateFileAtPath(Path.Combine(path, "bin", "xr_3da.exe"));
        return path;
    }

    private string CreateFile(string relativePath)
    {
        return CreateFileAtPath(Path.Combine(_root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
    }

    private static string CreateFileAtPath(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "test");
        return path;
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private sealed class FakeWorkspaceManager : IProfileWorkspaceManager
    {
        public void DeleteProfileWorkspace(ModProfile profile, string gamePath)
        {
        }
    }
}

using StalkerModLauncher.Models;
using StalkerModLauncher.Services;
using Xunit;

namespace StalkerModLauncher.Tests;

public sealed class GameExitDiagnosticsServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "StalkerModLauncherTests",
        Guid.NewGuid().ToString("N"));
    private readonly GameExitDiagnosticsService _service = new(new ProfileDataPathResolver());

    [Fact]
    public void Analyze_ReportsQuickExitAndFreshDiagnosticFiles()
    {
        var started = DateTime.UtcNow.AddSeconds(-5);
        var logsPath = Path.Combine(_root, "userdata", "logs");
        var log = CreateFile(logsPath, "xray.log", started.AddSeconds(2));
        var dump = CreateFile(logsPath, "xray.mdmp", started.AddSeconds(3));
        var profile = new ModProfile { WorkspacePath = _root };
        var session = new GameSessionResult(TimeSpan.FromSeconds(5), true, -1, started, DateTime.UtcNow);

        var result = _service.Analyze(profile, session);

        Assert.True(result.IsQuickExit);
        Assert.Equal(-1, result.ExitCode);
        Assert.True(result.IsSuspiciousExit);
        Assert.Equal(log, result.LatestLogPath);
        Assert.Equal(dump, result.LatestCrashDumpPath);
    }

    [Fact]
    public void Analyze_IgnoresOldFilesAndDoesNotFlagLongSession()
    {
        var started = DateTime.UtcNow.AddMinutes(-1);
        var logsPath = Path.Combine(_root, "userdata", "logs");
        CreateFile(logsPath, "old.log", started.AddMinutes(-10));
        var profile = new ModProfile { WorkspacePath = _root };
        var session = new GameSessionResult(TimeSpan.FromMinutes(1), true, 0, started, DateTime.UtcNow);

        var result = _service.Analyze(profile, session);

        Assert.False(result.IsQuickExit);
        Assert.False(result.IsSuspiciousExit);
        Assert.Null(result.LatestLogPath);
        Assert.Null(result.LatestCrashDumpPath);
    }

    [Fact]
    public void Analyze_HandlesMissingWorkspace()
    {
        var result = _service.Analyze(new ModProfile(), new GameSessionResult(TimeSpan.FromSeconds(1), false));

        Assert.True(result.IsQuickExit);
        Assert.Null(result.LatestLogPath);
    }

    [Fact]
    public void Analyze_FindsFreshStandaloneAppdataLog()
    {
        var started = DateTime.UtcNow.AddSeconds(-5);
        var modRoot = Path.Combine(_root, "standalone");
        var log = CreateFile(Path.Combine(modRoot, "appdata", "logs"), "xray.log", started.AddSeconds(2));
        var profile = new ModProfile { IsStandalone = true };
        profile.Mods.Add(new ModEntry { SourcePath = modRoot, IsEnabled = true });

        var result = _service.Analyze(
            profile,
            new GameSessionResult(TimeSpan.FromSeconds(5), true, -1, started, DateTime.UtcNow));

        Assert.Equal(log, result.LatestLogPath);
    }

    private static string CreateFile(string directory, string name, DateTime lastWriteUtc)
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, name);
        File.WriteAllText(path, "diagnostic");
        File.SetLastWriteTimeUtc(path, lastWriteUtc);
        return path;
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}

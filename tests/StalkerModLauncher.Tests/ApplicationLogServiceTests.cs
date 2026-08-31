using StalkerModLauncher.Services;
using StalkerModLauncher.Models;
using Xunit;

namespace StalkerModLauncher.Tests;

public sealed class ApplicationLogServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "StalkerModLauncherTests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public void WriteAppendsFileEntryAndReturnsDisplayEntry()
    {
        var service = new ApplicationLogService(new AppPaths(_root, Path.Combine(_root, "workspaces"), false));
        var timestamp = new DateTime(2026, 6, 8, 12, 34, 56);

        var display = service.Write("Test message", timestamp);

        Assert.Equal("[12:34:56] Test message", display);
        Assert.Contains("[2026-06-08 12:34:56] Test message", File.ReadAllText(Path.Combine(_root, "launcher.log")));
    }

    [Fact]
    public void WriteRotatesLargeFile()
    {
        Directory.CreateDirectory(_root);
        var logPath = Path.Combine(_root, "launcher.log");
        File.WriteAllBytes(logPath, new byte[1024 * 1024]);
        var service = new ApplicationLogService(new AppPaths(_root, Path.Combine(_root, "workspaces"), false));

        service.Write("After rotation", new DateTime(2026, 6, 8, 12, 35, 0));

        Assert.True(File.Exists(Path.Combine(_root, "launcher.old.log")));
        Assert.Contains("After rotation", File.ReadAllText(logPath));
        Assert.True(new FileInfo(logPath).Length < 1024 * 1024);
    }

    [Fact]
    public void ErrorsOnlyLevelSkipsOrdinaryFileEntriesButKeepsErrors()
    {
        var service = new ApplicationLogService(new AppPaths(_root, Path.Combine(_root, "workspaces"), false))
        {
            Level = LauncherLogLevel.ErrorsOnly
        };

        service.Write("Ordinary message", messageLevel: LauncherLogLevel.Standard);
        service.Write("Failure message", messageLevel: LauncherLogLevel.ErrorsOnly);

        var log = File.ReadAllText(Path.Combine(_root, "launcher.log"));
        Assert.DoesNotContain("Ordinary message", log);
        Assert.Contains("Failure message", log);
    }

    [Fact]
    public void ErrorsOnlyLevelDoesNotInferSeverityFromMessageText()
    {
        var service = new ApplicationLogService(new AppPaths(_root, Path.Combine(_root, "workspaces"), false))
        {
            Level = LauncherLogLevel.ErrorsOnly
        };

        service.Write("Launcher UI startup failed: test");

        Assert.False(File.Exists(Path.Combine(_root, "launcher.log")));
    }

    [Fact]
    public void DetailedLevelWritesEveryExplicitSeverity()
    {
        var service = new ApplicationLogService(new AppPaths(_root, Path.Combine(_root, "workspaces"), false))
        {
            Level = LauncherLogLevel.Detailed
        };

        service.Write("Error", messageLevel: LauncherLogLevel.ErrorsOnly);
        service.Write("Standard", messageLevel: LauncherLogLevel.Standard);
        service.Write("Detailed", messageLevel: LauncherLogLevel.Detailed);

        var log = File.ReadAllText(Path.Combine(_root, "launcher.log"));
        Assert.Contains("Error", log);
        Assert.Contains("Standard", log);
        Assert.Contains("Detailed", log);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}

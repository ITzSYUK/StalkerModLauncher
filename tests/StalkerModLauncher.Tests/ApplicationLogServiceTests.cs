using StalkerModLauncher.Services;
using Xunit;

namespace StalkerModLauncher.Tests;

public sealed class ApplicationLogServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "StalkerModLauncherTests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public void Write_AppendsFileEntryAndReturnsDisplayEntry()
    {
        var service = new ApplicationLogService(new AppPaths(_root, Path.Combine(_root, "workspaces"), false));
        var timestamp = new DateTime(2026, 6, 8, 12, 34, 56);

        var display = service.Write("Test message", timestamp);

        Assert.Equal("[12:34:56] Test message", display);
        Assert.Contains("[2026-06-08 12:34:56] Test message", File.ReadAllText(Path.Combine(_root, "launcher.log")));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}

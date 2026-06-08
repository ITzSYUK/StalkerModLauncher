using StalkerModLauncher.Models;
using StalkerModLauncher.Services;
using Xunit;

namespace StalkerModLauncher.Tests;

public sealed class ScreenshotScannerServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "StalkerModLauncherTests",
        Guid.NewGuid().ToString("N"));
    private readonly ProfileDataPathResolver _resolver = new();

    [Fact]
    public async Task ScanAsync_FindsSupportedImagesAcrossProfileAndGamePaths()
    {
        var workspace = Path.Combine(_root, "workspace");
        var game = Path.Combine(_root, "game");
        var profileScreenshot = CreateFile(workspace, "userdata", "screenshots", "profile.png");
        var gameScreenshot = CreateFile(game, "appdata", "screenshots", "game.jpg");
        CreateFile(game, "appdata", "screenshots", "ignored.dds");
        var profile = new ModProfile { WorkspacePath = workspace };
        var service = new ScreenshotScannerService(_resolver);

        var result = await service.ScanAsync(profile, game);

        Assert.Equal(2, result.Count);
        Assert.Contains(profileScreenshot, result);
        Assert.Contains(gameScreenshot, result);
    }

    [Fact]
    public async Task ScanAsync_UsesStandaloneDataLocations()
    {
        var screenshot = CreateFile(_root, "bin_x64", "_appdata_", "screenshots", "standalone.bmp");
        var profile = new ModProfile { IsStandalone = true };
        profile.Mods.Add(new ModEntry { SourcePath = _root, IsEnabled = true });
        var service = new ScreenshotScannerService(_resolver);

        var result = await service.ScanAsync(profile, string.Empty);

        Assert.Contains(screenshot, result);
    }

    [Fact]
    public async Task ScanAsync_HonorsCancellation()
    {
        var profile = new ModProfile { WorkspacePath = _root };
        var service = new ScreenshotScannerService(_resolver);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.ScanAsync(profile, string.Empty, cancellation.Token));
    }

    private static string CreateFile(string root, params string[] parts)
    {
        var path = parts.Aggregate(root, Path.Combine);
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
}

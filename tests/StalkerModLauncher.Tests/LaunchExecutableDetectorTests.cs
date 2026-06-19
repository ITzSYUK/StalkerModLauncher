using StalkerModLauncher.Services;
using StalkerModLauncher.Models;
using Xunit;

namespace StalkerModLauncher.Tests;

public sealed class LaunchExecutableDetectorTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "StalkerModLauncherTests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public void DetectBest_PrefersLaterModWhenSameExecutableExistsInPatch()
    {
        var game = CreateDirectory("game");
        var mainMod = CreateDirectory("main-mod");
        var patch = CreateDirectory("patch");
        CreateFile(game, "bin/xr_3da.exe");
        CreateFile(mainMod, "bin_x64/xrEngine.exe");
        var patchExecutable = CreateFile(patch, "bin_x64/xrEngine.exe");

        var detected = LaunchExecutableDetector.DetectBest(
            [
                new LaunchExecutableSearchRoot(game, "базовая игра", 0),
                new LaunchExecutableSearchRoot(mainMod, "мод: main", 1),
                new LaunchExecutableSearchRoot(patch, "мод: patch", 2)
            ],
            @"bin_x64\xrEngine.exe");

        Assert.NotNull(detected);
        Assert.Equal(patchExecutable, detected.FullPath);
        Assert.Equal("мод: patch", detected.SourceName);
        Assert.Equal(@"bin_x64\xrEngine.exe", detected.RelativePath);
    }

    [Fact]
    public void DetectBest_IgnoresDedicatedServerExecutableByDefault()
    {
        var game = CreateDirectory("game");
        CreateFile(game, "bin/dedicated/XR_3DA.exe");

        var detected = LaunchExecutableDetector.DetectBest(
            [new LaunchExecutableSearchRoot(game, "базовая игра", 0)],
            @"bin\XR_3DA.exe");

        Assert.Null(detected);
    }

    [Fact]
    public void DetectBest_PrefersAnomalyLauncherForStandaloneBuilds()
    {
        var anomaly = CreateDirectory("anomaly");
        var launcher = CreateFile(anomaly, "AnomalyLauncher.exe");
        CreateFile(anomaly, "bin/AnomalyDX10.exe");

        var detected = LaunchExecutableDetector.DetectBest(
            [new LaunchExecutableSearchRoot(anomaly, "автономный мод", 1)],
            requestedRelativePath: null);

        Assert.NotNull(detected);
        Assert.Equal(launcher, detected.FullPath);
        Assert.Equal("найден лаунчер автономной сборки", detected.Reason);
    }

    [Fact]
    public void DetectAutomaticSelection_IgnoresPreviouslyPinnedBaseGameExecutable()
    {
        var game = CreateDirectory("game");
        var mainMod = CreateDirectory("main-mod");
        var patch = CreateDirectory("patch");
        CreateFile(game, "bin/xr_3da.exe");
        CreateFile(mainMod, "bin_x64/xrEngine.exe");
        CreateFile(patch, "bin_x64/xrEngine.exe");
        var profile = new ModProfile
        {
            Name = "Liquidation",
            GameInstallPath = game,
            ExecutableRelativePath = @"bin\xr_3da.exe",
            ExecutableSourcePath = game,
            Mods =
            {
                new ModEntry
                {
                    Name = "main",
                    SourcePath = mainMod,
                    IsEnabled = true,
                    Order = 1
                },
                new ModEntry
                {
                    Name = "patch",
                    SourcePath = patch,
                    IsEnabled = true,
                    Order = 2
                }
            }
        };

        var selection = ProfileExecutableSourceResolver.DetectAutomaticSelection(
            profile,
            includeWorkspace: false);

        Assert.NotNull(selection);
        Assert.Equal(@"bin_x64\xrEngine.exe", selection.RelativePath);
        Assert.Equal("мод: patch", selection.SourceName);
        Assert.False(selection.PinsSource);
    }

    private string CreateDirectory(string relativePath)
    {
        var path = Path.Combine(_root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(path);
        return path;
    }

    private static string CreateFile(string root, string relativePath)
    {
        var path = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
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

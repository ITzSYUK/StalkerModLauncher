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
    public void DetectBestPrefersLaterModWhenSameExecutableExistsInPatch()
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
    public void DetectBestIgnoresDedicatedServerExecutableByDefault()
    {
        var game = CreateDirectory("game");
        CreateFile(game, "bin/dedicated/XR_3DA.exe");

        var detected = LaunchExecutableDetector.DetectBest(
            [new LaunchExecutableSearchRoot(game, "базовая игра", 0)],
            @"bin\XR_3DA.exe");

        Assert.Null(detected);
    }

    [Fact]
    public void DetectBestPrefersAnomalyLauncherForStandaloneBuilds()
    {
        var anomaly = CreateDirectory("anomaly");
        var launcher = CreateFile(anomaly, "AnomalyLauncher.exe");
        CreateFile(anomaly, "bin/AnomalyDX10.exe");

        var detected = LaunchExecutableDetector.DetectBest(
            [new LaunchExecutableSearchRoot(anomaly, "автономная сборка", 1)],
            requestedRelativePath: null);

        Assert.NotNull(detected);
        Assert.Equal(launcher, detected.FullPath);
        Assert.Equal("найден лаунчер автономной сборки", detected.Reason);
    }

    [Fact]
    public void DetectBestRecognizesOgsrEngineInBinVariantDirectory()
    {
        var game = CreateDirectory("game");
        var mod = CreateDirectory("ogsr-mod");
        CreateFile(game, "bin/xr_3da.exe");
        var engine = CreateFile(mod, "bin_OGSR/xrEngine.exe");

        var detected = LaunchExecutableDetector.DetectBest(
            [
                new LaunchExecutableSearchRoot(game, "базовая игра", 0),
                new LaunchExecutableSearchRoot(mod, "мод: OGSR", 1)
            ],
            requestedRelativePath: null);

        Assert.NotNull(detected);
        Assert.Equal(engine, detected.FullPath);
        Assert.Equal(@"bin_OGSR\xrEngine.exe", detected.RelativePath);
        Assert.Equal("найден движок OGSR/X-Ray в каталоге bin_*", detected.Reason);
    }

    [Fact]
    public void DetectBestPrefersHigherPriorityOgsrEngineOverLowerBinX64Engine()
    {
        var lowerMod = CreateDirectory("lower-mod");
        var higherMod = CreateDirectory("higher-mod");
        CreateFile(lowerMod, "bin_x64/xrEngine.exe");
        var higherEngine = CreateFile(higherMod, "bin_OGSR/xrEngine.exe");

        var detected = LaunchExecutableDetector.DetectBest(
            [
                new LaunchExecutableSearchRoot(lowerMod, "мод: lower", 1),
                new LaunchExecutableSearchRoot(higherMod, "мод: higher", 2)
            ],
            requestedRelativePath: null);

        Assert.NotNull(detected);
        Assert.Equal(higherEngine, detected.FullPath);
        Assert.Equal(@"bin_OGSR\xrEngine.exe", detected.RelativePath);
        Assert.Equal("мод: higher", detected.SourceName);
    }

    [Fact]
    public void DetectBestDoesNotPreferHigherPriorityUnrecognizedToolOverEngine()
    {
        var engineMod = CreateDirectory("engine-mod");
        var toolMod = CreateDirectory("tool-mod");
        var engine = CreateFile(engineMod, "bin_x64/xrEngine.exe");
        CreateFile(toolMod, "tools/configurator.exe");

        var detected = LaunchExecutableDetector.DetectBest(
            [
                new LaunchExecutableSearchRoot(engineMod, "мод: engine", 1),
                new LaunchExecutableSearchRoot(toolMod, "мод: tool", 2)
            ],
            requestedRelativePath: null);

        Assert.NotNull(detected);
        Assert.Equal(engine, detected.FullPath);
    }

    [Fact]
    public void DetectAutomaticSelectionIgnoresPreviouslyPinnedBaseGameExecutable()
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

    [Fact]
    public void ExistingAutomaticSelectionUsesHighestPriorityProviderWithoutRecursiveScan()
    {
        var game = CreateDirectory("game");
        var lowerMod = CreateDirectory("lower-mod");
        var higherMod = CreateDirectory("higher-mod");
        CreateFile(game, "bin/xr_3da.exe");
        CreateFile(lowerMod, "bin_OGSR/xrEngine.exe");
        CreateFile(higherMod, "bin_OGSR/xrEngine.exe");
        var profile = new ModProfile
        {
            GameInstallPath = game,
            ExecutableRelativePath = @"bin_OGSR\xrEngine.exe",
            Mods =
            {
                new ModEntry { Name = "lower", SourcePath = lowerMod, IsEnabled = true, Order = 1 },
                new ModEntry { Name = "higher", SourcePath = higherMod, IsEnabled = true, Order = 2 }
            }
        };

        var selection = ProfileExecutableSourceResolver.TryResolveExistingAutomaticSelection(profile);

        Assert.NotNull(selection);
        Assert.Equal(@"bin_OGSR\xrEngine.exe", selection.RelativePath);
        Assert.Equal("мод: higher", selection.SourceName);
        Assert.False(selection.PinsSource);
    }

    [Fact]
    public void ExistingAutomaticSelectionReturnsNullWhenStoredExecutableNoLongerExists()
    {
        var game = CreateDirectory("game");
        var profile = new ModProfile
        {
            GameInstallPath = game,
            ExecutableRelativePath = @"bin\missing.exe"
        };

        var selection = ProfileExecutableSourceResolver.TryResolveExistingAutomaticSelection(profile);

        Assert.Null(selection);
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

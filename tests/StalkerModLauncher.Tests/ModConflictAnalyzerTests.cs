using StalkerModLauncher.Services;
using Xunit;

namespace StalkerModLauncher.Tests;

public sealed class ModConflictAnalyzerTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "StalkerModLauncherTests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task AnalyzeAsync_MarksOverlappingModsAccordingToLoadOrder()
    {
        var first = CreateMod("first", "gamedata/config/shared.ltx", "gamedata/config/first.ltx");
        var second = CreateMod("second", "gamedata/config/shared.ltx");
        var analyzer = new ModConflictAnalyzer();

        var result = await analyzer.AnalyzeAsync(
        [
            new ModConflictInput("first", "First", first, true),
            new ModConflictInput("second", "Second", second, true)
        ]);

        Assert.True(result["first"].IsLocked);
        Assert.False(result["first"].HasOverlapsAbove);
        Assert.False(result["second"].IsLocked);
        Assert.True(result["second"].HasOverlapsAbove);
        Assert.Equal(1, result["second"].OverwrittenFileCount);
        Assert.Equal(["First"], result["second"].OverwrittenModNames);
    }

    [Fact]
    public async Task AnalyzeAsync_IgnoresDisabledMods()
    {
        var first = CreateMod("first", "shared.ltx");
        var second = CreateMod("second", "shared.ltx");
        var analyzer = new ModConflictAnalyzer();

        var result = await analyzer.AnalyzeAsync(
        [
            new ModConflictInput("first", "First", first, true),
            new ModConflictInput("second", "Second", second, false)
        ]);

        Assert.False(result["first"].IsLocked);
        Assert.False(result["second"].HasOverlapsAbove);
    }

    [Fact]
    public async Task AnalyzeAsync_CountsUniqueOverwrittenFilesAndSourceMods()
    {
        var first = CreateMod("first", "shared-a.ltx", "shared-b.ltx");
        var second = CreateMod("second", "shared-b.ltx", "shared-c.ltx");
        var patch = CreateMod("patch", "shared-a.ltx", "shared-b.ltx", "shared-c.ltx");
        var analyzer = new ModConflictAnalyzer();

        var result = await analyzer.AnalyzeAsync(
        [
            new ModConflictInput("first", "Main mod", first, true),
            new ModConflictInput("second", "Addon", second, true),
            new ModConflictInput("patch", "Patch", patch, true)
        ]);

        Assert.Equal(3, result["patch"].OverwrittenFileCount);
        Assert.Equal(["Main mod", "Addon"], result["patch"].OverwrittenModNames);
    }

    [Fact]
    public async Task AnalyzeAsync_MarksLastEnabledProviderOfLaunchExecutable()
    {
        var main = CreateMod("main", "bin_x64/xrEngine.exe");
        var patch = CreateMod("patch", "bin_x64/xrEngine.exe");
        var disabledHotfix = CreateMod("disabled", "bin_x64/xrEngine.exe");
        var analyzer = new ModConflictAnalyzer();

        var result = await analyzer.AnalyzeAsync(
        [
            new ModConflictInput("main", "Main mod", main, true),
            new ModConflictInput("patch", "Patch", patch, true),
            new ModConflictInput("disabled", "Disabled hotfix", disabledHotfix, false)
        ], @"bin_x64\xrEngine.exe");

        Assert.False(result["main"].ProvidesLaunchExecutable);
        Assert.True(result["patch"].ProvidesLaunchExecutable);
        Assert.False(result["disabled"].ProvidesLaunchExecutable);
    }

    [Fact]
    public async Task AnalyzeAsync_ClassifiesConfigurationAndBinaryOverlays()
    {
        var main = CreateMod("main", "gamedata/config/system.ltx", "bin/xrCore.dll", "textures/test.dds");
        var patch = CreateMod("patch", "gamedata/config/system.ltx", "bin/xrCore.dll", "textures/test.dds");
        var analyzer = new ModConflictAnalyzer();

        var result = await analyzer.AnalyzeAsync(
        [
            new ModConflictInput("main", "Main", main, true),
            new ModConflictInput("patch", "Patch", patch, true)
        ]);

        Assert.Equal(1, result["patch"].OverwrittenConfigurationCount);
        Assert.Equal(1, result["patch"].OverwrittenBinaryCount);
        Assert.Equal(3, result["patch"].OverwrittenFileCount);
    }

    private string CreateMod(string name, params string[] relativeFiles)
    {
        var modPath = Path.Combine(_root, name);
        foreach (var relativeFile in relativeFiles)
        {
            var filePath = Path.Combine(modPath, relativeFile);
            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
            File.WriteAllText(filePath, name);
        }

        return modPath;
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}

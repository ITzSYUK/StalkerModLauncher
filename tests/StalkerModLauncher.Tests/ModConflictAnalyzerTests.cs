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
            new ModConflictInput("first", first, true),
            new ModConflictInput("second", second, true)
        ]);

        Assert.True(result["first"].IsLocked);
        Assert.False(result["first"].HasOverlapsAbove);
        Assert.False(result["second"].IsLocked);
        Assert.True(result["second"].HasOverlapsAbove);
    }

    [Fact]
    public async Task AnalyzeAsync_IgnoresDisabledMods()
    {
        var first = CreateMod("first", "shared.ltx");
        var second = CreateMod("second", "shared.ltx");
        var analyzer = new ModConflictAnalyzer();

        var result = await analyzer.AnalyzeAsync(
        [
            new ModConflictInput("first", first, true),
            new ModConflictInput("second", second, false)
        ]);

        Assert.False(result["first"].IsLocked);
        Assert.False(result["second"].HasOverlapsAbove);
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

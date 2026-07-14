using StalkerModLauncher.Services;
using Xunit;

namespace StalkerModLauncher.Tests;

public sealed class ModScannerServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "StalkerModLauncherTests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task ScanFolderAsync_FindsMultipleNestedMods()
    {
        CreateFile("wrapper/first/fsgame.ltx");
        Directory.CreateDirectory(Path.Combine(_root, "wrapper", "second", "gamedata"));
        CreateFile("other/third/bin_x64/xrEngine.exe");
        var scanner = new ModScannerService();

        var result = await scanner.ScanFolderAsync(_root);

        Assert.Equal(3, result.Count);
        Assert.Contains(result, mod => mod.Name == "first" && mod.DetectedBy.Contains("fsgame.ltx"));
        Assert.Contains(result, mod => mod.Name == "second" && mod.DetectedBy.Contains("gamedata"));
        Assert.Contains(result, mod => mod.Name == "third" && mod.DetectedBy.Contains("bin_x64"));
    }

    [Fact]
    public async Task ScanFolderAsync_DoesNotReportChildrenInsideDetectedMod()
    {
        CreateFile("parent/fsgame.ltx");
        CreateFile("parent/nested/fsgame.ltx");
        var scanner = new ModScannerService();

        var result = await scanner.ScanFolderAsync(_root);

        Assert.Single(result);
        Assert.Equal("parent", result[0].Name);
    }

    [Fact]
    public async Task ScanFolderAsync_FindsAnomalyArchiveModInsideDbMods()
    {
        CreateFile("Осень/db/mods/anomaly-autumn-dark.db0");
        CreateFile("Осень/meta.ini");
        var scanner = new ModScannerService();

        var result = await scanner.ScanFolderAsync(_root);

        var mod = Assert.Single(result);
        Assert.Equal("Осень", mod.Name);
        Assert.Contains("anomaly-autumn-dark.db0", mod.DetectedBy);
    }

    [Fact]
    public async Task ScanFolderAsync_UsesPatchRootForArchiveInsidePatchesDirectory()
    {
        CreateFile("SNW Patch 1.09b (db)/patches/xpatch_03_snw8.db");
        var scanner = new ModScannerService();

        var result = await scanner.ScanFolderAsync(_root);

        var mod = Assert.Single(result);
        Assert.Equal("SNW Patch 1.09b (db)", mod.Name);
        Assert.Equal(
            Path.Combine(_root, "SNW Patch 1.09b (db)"),
            mod.Path);
        Assert.Contains(Path.Combine("patches", "xpatch_03_snw8.db"), mod.DetectedBy);
    }

    [Fact]
    public async Task ScanFolderAsync_ReturnsEmptyForMissingFolder()
    {
        var scanner = new ModScannerService();

        var result = await scanner.ScanFolderAsync(Path.Combine(_root, "missing"));

        Assert.Empty(result);
    }

    private void CreateFile(string relativePath)
    {
        var path = Path.Combine(_root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, string.Empty);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}

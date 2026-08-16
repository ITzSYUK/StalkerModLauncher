using StalkerModLauncher.Models;
using StalkerModLauncher.Services;
using Xunit;

namespace StalkerModLauncher.Tests;

public sealed class ProfileReadinessServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "StalkerModLauncherTests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public void ValidateAcceptsConfiguredOverlayProfile()
    {
        CreateFile("game/fsgame.ltx");
        CreateFile("game/bin/xr_3da.exe");
        var profile = new ModProfile { GameInstallPath = Path.Combine(_root, "game") };

        var result = ProfileReadinessService.Validate(profile);

        Assert.True(result.IsValid);
        Assert.Equal("Готов к запуску.", result.Summary);
    }

    [Fact]
    public void ValidateRejectsOverlayProfileWithoutOwnGamePath()
    {
        CreateFile("default-game/fsgame.ltx");
        CreateFile("default-game/bin/xr_3da.exe");
        var profile = new ModProfile { GameInstallPath = string.Empty };

        var result = ProfileReadinessService.Validate(profile);

        Assert.False(result.IsValid);
        Assert.Contains("Выберите папку с установленной игрой.", result.Summary);
    }

    [Fact]
    public void ValidateRejectsMissingEnabledMod()
    {
        CreateFile("game/fsgame.ltx");
        CreateFile("game/bin/xr_3da.exe");
        var profile = new ModProfile { GameInstallPath = Path.Combine(_root, "game") };
        profile.Mods.Add(new ModEntry { Name = "Missing", SourcePath = Path.Combine(_root, "missing") });

        var result = ProfileReadinessService.Validate(profile);

        Assert.False(result.IsValid);
        Assert.Contains("Папка мода не найдена: Missing", result.Summary);
    }

    [Fact]
    public void ValidateRejectsMissingMo2OverwriteLayer()
    {
        CreateFile("game/fsgame.ltx");
        CreateFile("game/bin/xr_3da.exe");
        var profile = new ModProfile
        {
            GameInstallPath = Path.Combine(_root, "game"),
            Mo2OverwritePath = Path.Combine(_root, "missing-overwrite")
        };

        var result = ProfileReadinessService.Validate(profile);

        Assert.False(result.IsValid);
        Assert.Contains("Папка MO2 overwrite не найдена", result.Summary);
    }

    [Fact]
    public void ValidateRequiresExactlyOneStandaloneModAndSafeExecutable()
    {
        var modPath = Path.Combine(_root, "mod");
        Directory.CreateDirectory(modPath);
        var profile = new ModProfile { IsStandalone = true, ExecutableRelativePath = @"..\outside.exe" };
        profile.Mods.Add(new ModEntry { Name = "Standalone", SourcePath = modPath });

        var result = ProfileReadinessService.Validate(profile);

        Assert.False(result.IsValid);
        Assert.Contains("must not leave", result.Summary);
    }

    [Fact]
    public void ValidateRejectsExcludedUniqueFileWithoutFallbackProvider()
    {
        CreateFile("game/fsgame.ltx");
        CreateFile("game/bin/xr_3da.exe");
        CreateFile("mod/unique.ltx");
        var profile = new ModProfile { GameInstallPath = Path.Combine(_root, "game") };
        profile.Mods.Add(new ModEntry
        {
            Name = "Mod",
            SourcePath = Path.Combine(_root, "mod"),
            ExcludedFiles = ["unique.ltx"]
        });

        var result = ProfileReadinessService.Validate(profile);

        Assert.False(result.IsValid);
        Assert.Contains("больше не имеет другого поставщика", result.Summary);
    }

    [Fact]
    public void ValidateUsesCommonReadySummaryForStandaloneProfile()
    {
        var modPath = Path.Combine(_root, "standalone");
        Directory.CreateDirectory(modPath);
        var profile = new ModProfile { IsStandalone = true };
        profile.Mods.Add(new ModEntry { Name = "Standalone", SourcePath = modPath });

        var result = ProfileReadinessService.Validate(profile);

        Assert.True(result.IsValid);
        Assert.Equal("Готов к запуску.", result.Summary);
    }

    private void CreateFile(string relativePath)
    {
        var path = Path.Combine(_root, relativePath.Replace('/', Path.DirectorySeparatorChar));
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

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
    private readonly ProfileReadinessService _service = new(new GameInstallationValidator());

    [Fact]
    public void Validate_AcceptsConfiguredOverlayProfile()
    {
        CreateFile("game/fsgame.ltx");
        CreateFile("game/bin/xr_3da.exe");
        var profile = new ModProfile { GameInstallPath = Path.Combine(_root, "game") };

        var result = _service.Validate(profile, string.Empty);

        Assert.True(result.IsValid);
        Assert.Equal("Готов к запуску.", result.Summary);
    }

    [Fact]
    public void Validate_RejectsMissingEnabledMod()
    {
        CreateFile("game/fsgame.ltx");
        CreateFile("game/bin/xr_3da.exe");
        var profile = new ModProfile { GameInstallPath = Path.Combine(_root, "game") };
        profile.Mods.Add(new ModEntry { Name = "Missing", SourcePath = Path.Combine(_root, "missing") });

        var result = _service.Validate(profile, string.Empty);

        Assert.False(result.IsValid);
        Assert.Contains("Папка мода не найдена: Missing", result.Summary);
    }

    [Fact]
    public void Validate_RequiresExactlyOneStandaloneModAndSafeExecutable()
    {
        var modPath = Path.Combine(_root, "mod");
        Directory.CreateDirectory(modPath);
        var profile = new ModProfile { IsStandalone = true, ExecutableRelativePath = @"..\outside.exe" };
        profile.Mods.Add(new ModEntry { Name = "Standalone", SourcePath = modPath });

        var result = _service.Validate(profile, string.Empty);

        Assert.False(result.IsValid);
        Assert.Contains("must not leave", result.Summary);
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

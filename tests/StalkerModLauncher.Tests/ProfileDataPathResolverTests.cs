using StalkerModLauncher.Models;
using StalkerModLauncher.Services;
using Xunit;

namespace StalkerModLauncher.Tests;

public sealed class ProfileDataPathResolverTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "StalkerModLauncherTests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public void GetLogDirectoriesUsesWorkspaceForOverlayProfile()
    {
        var profile = new ModProfile { WorkspacePath = _root };

        var result = ProfileDataPathResolver.GetLogDirectories(profile);

        Assert.Equal([Path.Combine(_root, "userdata", "logs")], result);
    }

    [Fact]
    public void GetLogDirectoriesIncludesStandardStandaloneLocations()
    {
        Directory.CreateDirectory(_root);
        var profile = CreateStandaloneProfile();

        var result = ProfileDataPathResolver.GetLogDirectories(profile);

        Assert.Contains(Path.Combine(_root, "appdata", "logs"), result);
        Assert.Contains(Path.Combine(_root, "bin_x64", "_appdata_", "logs"), result);
    }

    [Fact]
    public void GetLogDirectoriesResolvesFourPartFsgameAppDataRoot()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(
            Path.Combine(_root, "fsgame.ltx"),
            "$app_data_root$ = true | false | $fs_root$ | custom_data");
        var profile = CreateStandaloneProfile();

        var result = ProfileDataPathResolver.GetLogDirectories(profile);

        Assert.Contains(Path.Combine(_root, "custom_data", "logs"), result);
    }

    [Fact]
    public void GetScreenshotDirectoriesUsesResolvedDataRoots()
    {
        Directory.CreateDirectory(_root);
        var profile = CreateStandaloneProfile();

        var result = ProfileDataPathResolver.GetScreenshotDirectories(profile);

        Assert.Contains(Path.Combine(_root, "appdata", "screenshots"), result);
        Assert.Contains(Path.Combine(_root, "bin_x64", "_appdata_", "screenshots"), result);
    }

    private ModProfile CreateStandaloneProfile()
    {
        var profile = new ModProfile { IsStandalone = true };
        profile.Mods.Add(new ModEntry { SourcePath = _root, IsEnabled = true });
        return profile;
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}

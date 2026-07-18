using StalkerModLauncher.Models;
using StalkerModLauncher.Services;
using Xunit;

namespace StalkerModLauncher.Tests;

public sealed class AppSettingsNormalizerTests
{
    [Fact]
    public void Normalize_ResetsUnknownLaunchBackendToLinkedWorkspace()
    {
        var profile = new ModProfile { LaunchBackendKind = (LaunchBackendKind)999 };
        var settings = new AppSettings { Profiles = [profile] };

        var normalized = AppSettingsNormalizer.Normalize(settings);

        Assert.Equal(LaunchBackendKind.LinkedWorkspace, normalized.Profiles[0].LaunchBackendKind);
    }

    [Fact]
    public void Normalize_PreservesVirtualFileSystemSelectionWithoutRuntime()
    {
        var profile = new ModProfile
        {
            LaunchBackendKind = LaunchBackendKind.VirtualFileSystem
        };
        var settings = new AppSettings { Profiles = [profile] };

        var normalized = AppSettingsNormalizer.Normalize(settings);

        Assert.Equal(LaunchBackendKind.VirtualFileSystem, normalized.Profiles[0].LaunchBackendKind);
    }

    [Fact]
    public void Normalize_ClearsUnsupportedAnomalyUsvfsOverride()
    {
        var profile = new ModProfile
        {
            UsvfsExecutableOverrideRelativePath = @"bin\Unknown.exe"
        };
        var settings = new AppSettings { Profiles = [profile] };

        var normalized = AppSettingsNormalizer.Normalize(settings);

        Assert.Empty(normalized.Profiles[0].UsvfsExecutableOverrideRelativePath);
    }

    [Fact]
    public void Normalize_PreservesPdaInterfacePreference()
    {
        var settings = new AppSettings { IsPdaInterfaceEnabled = true };

        var normalized = AppSettingsNormalizer.Normalize(settings);

        Assert.True(normalized.IsPdaInterfaceEnabled);
    }
}

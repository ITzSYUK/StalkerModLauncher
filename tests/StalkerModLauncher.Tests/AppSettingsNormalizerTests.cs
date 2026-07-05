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
    public void Normalize_ResetsLegacyVirtualFileSystemProfiles()
    {
        var profile = new ModProfile
        {
            LaunchBackendKind = LaunchBackendKind.VirtualFileSystem
        };
        var settings = new AppSettings { Profiles = [profile] };

        var normalized = AppSettingsNormalizer.Normalize(settings);

        Assert.Equal(LaunchBackendKind.LinkedWorkspace, normalized.Profiles[0].LaunchBackendKind);
    }
}

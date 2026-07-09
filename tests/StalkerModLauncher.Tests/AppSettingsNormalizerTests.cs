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
        var previous = Environment.GetEnvironmentVariable(UsvfsFeatureGate.EnableEnvironmentVariable);
        Environment.SetEnvironmentVariable(UsvfsFeatureGate.EnableEnvironmentVariable, null);
        try
        {
        var profile = new ModProfile
        {
            LaunchBackendKind = LaunchBackendKind.VirtualFileSystem
        };
        var settings = new AppSettings { Profiles = [profile] };

        var normalized = AppSettingsNormalizer.Normalize(settings);

        Assert.Equal(LaunchBackendKind.LinkedWorkspace, normalized.Profiles[0].LaunchBackendKind);
        }
        finally
        {
            Environment.SetEnvironmentVariable(UsvfsFeatureGate.EnableEnvironmentVariable, previous);
        }
    }

    [Fact]
    public void Normalize_KeepsVirtualFileSystemProfilesWhenOfficialUsvfsIsEnabled()
    {
        var previous = Environment.GetEnvironmentVariable(UsvfsFeatureGate.EnableEnvironmentVariable);
        Environment.SetEnvironmentVariable(UsvfsFeatureGate.EnableEnvironmentVariable, "1");
        try
        {
            var profile = new ModProfile
            {
                LaunchBackendKind = LaunchBackendKind.VirtualFileSystem
            };
            var settings = new AppSettings { Profiles = [profile] };

            var normalized = AppSettingsNormalizer.Normalize(settings);

            Assert.Equal(LaunchBackendKind.VirtualFileSystem, normalized.Profiles[0].LaunchBackendKind);
        }
        finally
        {
            Environment.SetEnvironmentVariable(UsvfsFeatureGate.EnableEnvironmentVariable, previous);
        }
    }
}

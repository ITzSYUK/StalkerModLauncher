using StalkerModLauncher.Models;
using StalkerModLauncher.Services;
using StalkerModLauncher.ViewModels;
using Xunit;

namespace StalkerModLauncher.Tests;

public sealed class ProfileSettingsViewModelTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "StalkerModLauncherProfileSettingsTests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Save_PreservesUsvfsBackendAndAnomalyRendererOverride()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, "AnomalyLauncher.exe"), string.Empty);
        var profile = new ModProfile
        {
            Name = "Anomaly",
            GameInstallPath = _root,
            ExecutableRelativePath = "AnomalyLauncher.exe"
        };
        var saved = false;
        var viewModel = new ProfileSettingsViewModel(
            profile,
            new DialogService(),
            () =>
            {
                saved = true;
                return Task.CompletedTask;
            },
            _ => null,
            () => null,
            _ => false,
            usvfsAvailable: true);

        viewModel.UseVirtualFileSystem = true;
        viewModel.UseAnomalyDx9 = true;
        viewModel.AnomalyUseAvx = true;

        var result = await viewModel.TrySaveAsync();

        Assert.True(result);
        Assert.True(saved);
        Assert.Equal(LaunchBackendKind.VirtualFileSystem, profile.LaunchBackendKind);
        Assert.Equal(@"bin\AnomalyDX9AVX.exe", profile.UsvfsExecutableOverrideRelativePath);
        Assert.Empty(profile.ExecutableSourcePath);
    }

    [Fact]
    public async Task Save_StandaloneProfileAlwaysUsesLinkedWorkspace()
    {
        var profile = new ModProfile
        {
            Name = "Standalone",
            ExecutableRelativePath = "game.exe",
            LaunchBackendKind = LaunchBackendKind.VirtualFileSystem
        };
        var viewModel = new ProfileSettingsViewModel(
            profile,
            new DialogService(),
            () => Task.CompletedTask,
            _ => null,
            () => null,
            _ => false,
            usvfsAvailable: true);

        viewModel.IsStandalone = true;
        var result = await viewModel.TrySaveAsync();

        Assert.True(result);
        Assert.Equal(LaunchBackendKind.LinkedWorkspace, profile.LaunchBackendKind);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}

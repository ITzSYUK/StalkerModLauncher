using System.Diagnostics;
using StalkerModLauncher.Models;
using StalkerModLauncher.Services;
using Xunit;

namespace StalkerModLauncher.Tests;

public sealed class UsvfsLaunchBackendTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "StalkerModLauncherUsvfsBackendTests",
        Guid.NewGuid().ToString("N"));
    private readonly string? _previousGateValue;

    public UsvfsLaunchBackendTests()
    {
        _previousGateValue = Environment.GetEnvironmentVariable(UsvfsFeatureGate.EnableEnvironmentVariable);
        Environment.SetEnvironmentVariable(UsvfsFeatureGate.EnableEnvironmentVariable, "1");
    }

    [Fact]
    public async Task PrepareAsync_BuildsVirtualLaunchPlanAndSessionStarter()
    {
        var game = CreateDirectory("game");
        var mod = CreateDirectory("mod");
        var workspace = CreateDirectory("workspace");
        File.WriteAllText(Path.Combine(game, "fsgame.ltx"), string.Empty);
        Directory.CreateDirectory(Path.Combine(mod, "bin_x64"));
        File.WriteAllText(Path.Combine(mod, "bin_x64", "xrEngine.exe"), string.Empty);
        File.WriteAllText(Path.Combine(game, UsvfsRuntimeFiles.DllFileName), string.Empty);
        File.WriteAllText(Path.Combine(game, UsvfsRuntimeFiles.ProxyFileName), string.Empty);

        var profile = new ModProfile
        {
            Id = "profile-usvfs",
            Name = "USVFS",
            GameInstallPath = game,
            ExecutableRelativePath = @"bin_x64\xrEngine.exe",
            LaunchBackendKind = LaunchBackendKind.VirtualFileSystem
        };
        profile.Mods.Add(new ModEntry
        {
            Id = "mod",
            Name = "Patch",
            SourcePath = mod,
            IsEnabled = true,
            Order = 1
        });
        var layerPlan = FileLayerPlan.CreateLinkedWorkspace(game, profile, workspace);
        var manifest = new OverlayManifestBuilder().BuildLinkedWorkspace(profile, layerPlan, workspace);
        var runtime = new RecordingUsvfsRuntime();
        var backend = new UsvfsLaunchBackend(runtime, game);

        var plan = await backend.PrepareAsync(
            new ProfileLaunchBackendContext(game, profile, layerPlan, manifest),
            new Progress<string>());

        Assert.Equal(LaunchBackendKind.VirtualFileSystem, plan.BackendKind);
        Assert.Equal(Path.Combine(mod, "bin_x64", "xrEngine.exe"), plan.ExecutablePath);
        Assert.Equal(game, plan.WorkingDirectory);
        Assert.NotNull(plan.RuntimeLease);
        Assert.NotNull(plan.ProcessStarter);
        Assert.NotNull(runtime.MappingPlan);
        Assert.Equal([game, mod, manifest.WriteOverlayRoot], runtime.MappingPlan.Operations.Select(operation => operation.SourcePath).ToArray());
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(UsvfsFeatureGate.EnableEnvironmentVariable, _previousGateValue);
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private string CreateDirectory(string relativePath)
    {
        var path = Path.Combine(_root, relativePath);
        Directory.CreateDirectory(path);
        return path;
    }

    private sealed class RecordingUsvfsRuntime : IUsvfsRuntime
    {
        public UsvfsMappingPlan? MappingPlan { get; private set; }

        public IUsvfsRuntimeSession CreateSession(
            UsvfsMappingPlan mappingPlan,
            UsvfsRuntimeOptions options,
            IProgress<string>? progress = null)
        {
            MappingPlan = mappingPlan;
            return new FakeUsvfsRuntimeSession();
        }

        public Task<UsvfsProcessLaunchResult> RunAsync(
            UsvfsMappingPlan mappingPlan,
            UsvfsProcessLaunchRequest launchRequest,
            UsvfsRuntimeOptions options,
            IProgress<string>? progress = null,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class FakeUsvfsRuntimeSession : IUsvfsRuntimeSession
    {
        public Process StartProcess(
            UsvfsProcessLaunchRequest launchRequest,
            IProgress<string>? progress = null,
            CancellationToken cancellationToken = default)
        {
            return Process.GetCurrentProcess();
        }

        public Task<int> GetExitCodeAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}

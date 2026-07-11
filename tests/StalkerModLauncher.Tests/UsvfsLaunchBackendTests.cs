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
        File.WriteAllText(
            Path.Combine(game, "fsgame.ltx"),
            "$app_data_root$ = true | false | $fs_root$ | _appdata_\\");
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
        Assert.EndsWith(Path.Combine("userdata", "usvfs-bootstrap", "bin_x64", "xrEngine.exe"), plan.ExecutablePath);
        Assert.Equal(string.Empty, File.ReadAllText(plan.ExecutablePath));
        var bootstrapRoot = Path.GetDirectoryName(Path.GetDirectoryName(plan.ExecutablePath))!;
        Assert.Equal(bootstrapRoot, plan.WorkingDirectory);
        Assert.NotNull(plan.RuntimeLease);
        Assert.NotNull(plan.ProcessStarter);
        Assert.NotNull(runtime.MappingPlan);
        Assert.Equal(
            [game, mod, manifest.WriteOverlayRoot],
            runtime.MappingPlan.Operations.Select(operation => operation.SourcePath).ToArray());
        Assert.Equal(bootstrapRoot, runtime.MappingPlan.VirtualRoot);
        var profileFsgame = Path.Combine(manifest.WriteOverlayRoot, "fsgame.ltx");
        Assert.True(File.Exists(profileFsgame));
        Assert.Contains(Path.Combine(workspace, "userdata"), File.ReadAllText(profileFsgame));
        Assert.Equal(File.ReadAllText(profileFsgame), File.ReadAllText(Path.Combine(bootstrapRoot, "fsgame.ltx")));
    }

    [Fact]
    public async Task PrepareAsync_Bypasses32BitAnomalyLauncherAndStartsConfigured64BitEngine()
    {
        var game = CreateDirectory("anomaly-game");
        var mod = CreateDirectory("anomaly-mod");
        var patch = CreateDirectory("anomaly-patch");
        var workspace = CreateDirectory("anomaly-workspace");
        File.WriteAllText(
            Path.Combine(game, "fsgame.ltx"),
            "$app_data_root$ = true | false | $fs_root$ | appdata\\");
        File.WriteAllLines(Path.Combine(game, "AnomalyLauncher.cfg"), ["DX11", "AVX", "1"]);
        File.WriteAllText(Path.Combine(game, "AnomalyLauncher.exe"), string.Empty);
        File.WriteAllText(Path.Combine(game, "commandline.txt"), "-smap2048");
        Directory.CreateDirectory(Path.Combine(game, "bin"));
        Directory.CreateDirectory(Path.Combine(mod, "bin"));
        File.WriteAllText(Path.Combine(game, "bin", "runtime.dll"), "base-runtime");
        File.WriteAllText(Path.Combine(game, "bin", "AnomalyDX9.exe"), "unused-engine");
        var engine = Path.Combine(mod, "bin", "AnomalyDX11AVX.exe");
        File.WriteAllText(engine, string.Empty);
        var dx9AvxEngine = Path.Combine(mod, "bin", "AnomalyDX9AVX.exe");
        File.WriteAllText(dx9AvxEngine, "lower-priority-dx9-avx-engine");
        File.WriteAllText(Path.Combine(mod, "bin", "feature.dll"), "mod-feature");
        Directory.CreateDirectory(Path.Combine(patch, "bin"));
        File.WriteAllText(
            Path.Combine(patch, "bin", "AnomalyDX9AVX.exe"),
            "higher-priority-dx9-avx-engine");
        File.WriteAllText(Path.Combine(game, UsvfsRuntimeFiles.DllFileName), string.Empty);
        File.WriteAllText(Path.Combine(game, UsvfsRuntimeFiles.ProxyFileName), string.Empty);

        var profile = new ModProfile
        {
            Id = "profile-anomaly-usvfs",
            Name = "Anomaly",
            GameInstallPath = game,
            ExecutableRelativePath = "AnomalyLauncher.exe",
            LaunchArguments = "-dbg",
            LaunchBackendKind = LaunchBackendKind.VirtualFileSystem
        };
        profile.Mods.Add(new ModEntry
        {
            Id = "anomaly-engine",
            Name = "Anomaly engine",
            SourcePath = mod,
            IsEnabled = true,
            Order = 1
        });
        profile.Mods.Add(new ModEntry
        {
            Id = "anomaly-engine-patch",
            Name = "Anomaly engine patch",
            SourcePath = patch,
            IsEnabled = true,
            Order = 2
        });
        var layerPlan = FileLayerPlan.CreateLinkedWorkspace(game, profile, workspace);
        var manifest = new OverlayManifestBuilder().BuildLinkedWorkspace(profile, layerPlan, workspace);
        var runtime = new RecordingUsvfsRuntime();
        var backend = new UsvfsLaunchBackend(runtime, game);

        var plan = await backend.PrepareAsync(
            new ProfileLaunchBackendContext(game, profile, layerPlan, manifest),
            new Progress<string>());

        Assert.EndsWith(Path.Combine("userdata", "usvfs-bootstrap", "bin", "AnomalyDX11AVX.exe"), plan.ExecutablePath);
        Assert.Equal(string.Empty, File.ReadAllText(plan.ExecutablePath));
        Assert.Equal("base-runtime", File.ReadAllText(Path.Combine(Path.GetDirectoryName(plan.ExecutablePath)!, "runtime.dll")));
        Assert.Equal("mod-feature", File.ReadAllText(Path.Combine(Path.GetDirectoryName(plan.ExecutablePath)!, "feature.dll")));
        Assert.False(File.Exists(Path.Combine(Path.GetDirectoryName(plan.ExecutablePath)!, "AnomalyDX9.exe")));
        Assert.Equal("-smap2048 -dbg", plan.Arguments);
        Assert.Equal(game, plan.WorkingDirectory);
        Assert.Equal(game, runtime.MappingPlan?.VirtualRoot);
        Assert.DoesNotContain(
            runtime.MappingPlan!.Operations,
            operation => FileSystemSafety.IsSameDirectory(operation.SourcePath, game));

        profile.UsvfsExecutableOverrideRelativePath = @"bin\AnomalyDX9AVX.exe";
        var overridePlan = await backend.PrepareAsync(
            new ProfileLaunchBackendContext(game, profile, layerPlan, manifest),
            new Progress<string>());

        Assert.EndsWith(Path.Combine("userdata", "usvfs-bootstrap", "bin", "AnomalyDX9AVX.exe"), overridePlan.ExecutablePath);
        Assert.Equal("higher-priority-dx9-avx-engine", File.ReadAllText(overridePlan.ExecutablePath));
        Assert.False(File.Exists(Path.Combine(Path.GetDirectoryName(overridePlan.ExecutablePath)!, "AnomalyDX11AVX.exe")));
    }

    [Fact]
    public async Task PrepareAsync_AllowsX86ExecutableWhenCrossArchitectureRuntimeIsPresent()
    {
        var game = CreateDirectory("x86-game");
        var workspace = CreateDirectory("x86-workspace");
        Directory.CreateDirectory(Path.Combine(game, "bin"));
        File.WriteAllText(
            Path.Combine(game, "fsgame.ltx"),
            "$app_data_root$ = true | false | $fs_root$ | appdata\\");
        var x86Executable = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            "SysWOW64",
            "cmd.exe");
        Assert.True(File.Exists(x86Executable));
        File.Copy(x86Executable, Path.Combine(game, "bin", "Play.exe"));
        File.WriteAllText(Path.Combine(game, UsvfsRuntimeFiles.DllFileName), string.Empty);
        File.WriteAllText(Path.Combine(game, UsvfsRuntimeFiles.ProxyFileName), string.Empty);
        File.WriteAllText(Path.Combine(game, UsvfsRuntimeFiles.X86DllFileName), string.Empty);
        File.WriteAllText(Path.Combine(game, UsvfsRuntimeFiles.X86ProxyFileName), string.Empty);
        File.WriteAllText(Path.Combine(game, UsvfsRuntimeFiles.X86HostFileName), string.Empty);
        var profile = new ModProfile
        {
            Id = "profile-x86-usvfs",
            Name = "x86",
            GameInstallPath = game,
            ExecutableRelativePath = @"bin\Play.exe",
            LaunchBackendKind = LaunchBackendKind.VirtualFileSystem
        };
        var layerPlan = FileLayerPlan.CreateLinkedWorkspace(game, profile, workspace);
        var manifest = new OverlayManifestBuilder().BuildLinkedWorkspace(profile, layerPlan, workspace);
        var backend = new UsvfsLaunchBackend(new RecordingUsvfsRuntime(), game);

        var plan = await backend.PrepareAsync(
            new ProfileLaunchBackendContext(game, profile, layerPlan, manifest),
            new Progress<string>());

        Assert.Equal(LaunchBackendKind.VirtualFileSystem, plan.BackendKind);
        Assert.Equal(WindowsExecutableArchitecture.X86, WindowsExecutableArchitectureDetector.Detect(plan.ExecutablePath));
        Assert.Equal(Path.Combine(game, "bin", "Play.exe"), plan.ExecutablePath);
        Assert.Equal(game, plan.WorkingDirectory);
    }

    [Fact]
    public async Task PrepareAsync_UsesPhysicalRootForBaseExecutableWithoutModEngineFiles()
    {
        var game = CreateDirectory("physical-root-game");
        var mod = CreateDirectory("physical-root-mod");
        var workspace = CreateDirectory("physical-root-workspace");
        Directory.CreateDirectory(Path.Combine(game, "bin"));
        Directory.CreateDirectory(Path.Combine(mod, "gamedata"));
        File.WriteAllText(Path.Combine(game, "fsgame.ltx"), "$app_data_root$ = true | false | userdata\\");
        Directory.CreateDirectory(Path.Combine(game, "userdata"));
        File.WriteAllText(Path.Combine(game, "userdata", "user.ltx"), "base user settings");
        var executable = Path.Combine(game, "bin", "xrEngine.exe");
        File.Copy(
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32", "cmd.exe"),
            executable);
        File.WriteAllText(Path.Combine(mod, "gamedata", "mod.txt"), "mod");
        File.WriteAllText(Path.Combine(game, UsvfsRuntimeFiles.DllFileName), string.Empty);
        File.WriteAllText(Path.Combine(game, UsvfsRuntimeFiles.ProxyFileName), string.Empty);
        var profile = new ModProfile
        {
            Id = "profile-physical-root",
            Name = "Physical root",
            GameInstallPath = game,
            ExecutableRelativePath = @"bin\xrEngine.exe",
            LaunchBackendKind = LaunchBackendKind.VirtualFileSystem
        };
        profile.Mods.Add(new ModEntry
        {
            Id = "data-mod",
            Name = "Data mod",
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

        Assert.Equal(executable, plan.ExecutablePath);
        Assert.Equal(game, plan.WorkingDirectory);
        Assert.Equal(game, runtime.MappingPlan?.VirtualRoot);
        Assert.DoesNotContain(
            runtime.MappingPlan!.Operations,
            operation => FileSystemSafety.IsSameDirectory(operation.SourcePath, game));
        Assert.Contains(
            runtime.MappingPlan.Operations,
            operation => FileSystemSafety.IsSameDirectory(operation.SourcePath, mod));
        Assert.Equal(
            "base user settings",
            File.ReadAllText(Path.Combine(workspace, "userdata", "user.ltx")));
    }

    [Fact]
    public async Task PrepareAsync_DoesNotOverwriteExistingProfileUserSettings()
    {
        var game = CreateDirectory("existing-user-game");
        var workspace = CreateDirectory("existing-user-workspace");
        Directory.CreateDirectory(Path.Combine(game, "bin_x64"));
        Directory.CreateDirectory(Path.Combine(game, "userdata"));
        Directory.CreateDirectory(Path.Combine(workspace, "userdata"));
        File.WriteAllText(Path.Combine(game, "fsgame.ltx"), "$app_data_root$ = true | false | userdata\\");
        File.WriteAllText(Path.Combine(game, "userdata", "user.ltx"), "base settings");
        File.WriteAllText(Path.Combine(workspace, "userdata", "user.ltx"), "profile settings");
        File.Copy(
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32", "cmd.exe"),
            Path.Combine(game, "bin_x64", "xrEngine.exe"));
        File.WriteAllText(Path.Combine(game, UsvfsRuntimeFiles.DllFileName), string.Empty);
        File.WriteAllText(Path.Combine(game, UsvfsRuntimeFiles.ProxyFileName), string.Empty);
        var profile = new ModProfile
        {
            Id = "profile-existing-user",
            Name = "Existing user",
            GameInstallPath = game,
            ExecutableRelativePath = @"bin_x64\xrEngine.exe",
            LaunchBackendKind = LaunchBackendKind.VirtualFileSystem
        };
        var layerPlan = FileLayerPlan.CreateLinkedWorkspace(game, profile, workspace);
        var manifest = new OverlayManifestBuilder().BuildLinkedWorkspace(profile, layerPlan, workspace);
        var backend = new UsvfsLaunchBackend(new RecordingUsvfsRuntime(), game);

        await backend.PrepareAsync(
            new ProfileLaunchBackendContext(game, profile, layerPlan, manifest),
            new Progress<string>());

        Assert.Equal(
            "profile settings",
            File.ReadAllText(Path.Combine(workspace, "userdata", "user.ltx")));
    }

    [Fact]
    public async Task PrepareAsync_KeepsBootstrapWhenModProvidesEngineDll()
    {
        var game = CreateDirectory("mod-engine-game");
        var mod = CreateDirectory("mod-engine-mod");
        var workspace = CreateDirectory("mod-engine-workspace");
        Directory.CreateDirectory(Path.Combine(game, "bin_x64"));
        Directory.CreateDirectory(Path.Combine(mod, "bin_x64"));
        File.WriteAllText(Path.Combine(game, "fsgame.ltx"), "$app_data_root$ = true | false | userdata\\");
        var executable = Path.Combine(game, "bin_x64", "xrEngine.exe");
        File.Copy(
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32", "cmd.exe"),
            executable);
        File.WriteAllText(Path.Combine(mod, "bin_x64", "engine_patch.dll"), "patch");
        File.WriteAllText(Path.Combine(game, UsvfsRuntimeFiles.DllFileName), string.Empty);
        File.WriteAllText(Path.Combine(game, UsvfsRuntimeFiles.ProxyFileName), string.Empty);
        var profile = new ModProfile
        {
            Id = "profile-mod-engine",
            Name = "Mod engine",
            GameInstallPath = game,
            ExecutableRelativePath = @"bin_x64\xrEngine.exe",
            LaunchBackendKind = LaunchBackendKind.VirtualFileSystem
        };
        profile.Mods.Add(new ModEntry
        {
            Id = "engine-mod",
            Name = "Engine mod",
            SourcePath = mod,
            IsEnabled = true,
            Order = 1
        });
        var layerPlan = FileLayerPlan.CreateLinkedWorkspace(game, profile, workspace);
        var manifest = new OverlayManifestBuilder().BuildLinkedWorkspace(profile, layerPlan, workspace);
        var backend = new UsvfsLaunchBackend(new RecordingUsvfsRuntime(), game);

        var plan = await backend.PrepareAsync(
            new ProfileLaunchBackendContext(game, profile, layerPlan, manifest),
            new Progress<string>());

        Assert.EndsWith(
            Path.Combine("userdata", "usvfs-bootstrap", "bin_x64", "xrEngine.exe"),
            plan.ExecutablePath);
        Assert.Equal("patch", File.ReadAllText(Path.Combine(Path.GetDirectoryName(plan.ExecutablePath)!, "engine_patch.dll")));
    }

    [Fact]
    public async Task PrepareAsync_RejectsX86ExecutableWhenX86RuntimeIsMissing()
    {
        var game = CreateDirectory("x86-missing-runtime-game");
        var workspace = CreateDirectory("x86-missing-runtime-workspace");
        Directory.CreateDirectory(Path.Combine(game, "bin"));
        File.WriteAllText(
            Path.Combine(game, "fsgame.ltx"),
            "$app_data_root$ = true | false | $fs_root$ | appdata\\");
        var x86Executable = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            "SysWOW64",
            "cmd.exe");
        Assert.True(File.Exists(x86Executable));
        File.Copy(x86Executable, Path.Combine(game, "bin", "Play.exe"));
        File.WriteAllText(Path.Combine(game, UsvfsRuntimeFiles.DllFileName), string.Empty);
        File.WriteAllText(Path.Combine(game, UsvfsRuntimeFiles.ProxyFileName), string.Empty);
        var profile = new ModProfile
        {
            Id = "profile-x86-missing-runtime",
            Name = "x86 missing runtime",
            GameInstallPath = game,
            ExecutableRelativePath = @"bin\Play.exe",
            LaunchBackendKind = LaunchBackendKind.VirtualFileSystem
        };
        var layerPlan = FileLayerPlan.CreateLinkedWorkspace(game, profile, workspace);
        var manifest = new OverlayManifestBuilder().BuildLinkedWorkspace(profile, layerPlan, workspace);
        var backend = new UsvfsLaunchBackend(new RecordingUsvfsRuntime(), game);

        var error = await Assert.ThrowsAsync<FileNotFoundException>(() => backend.PrepareAsync(
            new ProfileLaunchBackendContext(game, profile, layerPlan, manifest),
            new Progress<string>()));

        Assert.Contains(UsvfsRuntimeFiles.X86DllFileName, error.Message);
        Assert.Contains(UsvfsRuntimeFiles.X86HostFileName, error.Message);
        Assert.Contains("32-битной игры", error.Message);
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

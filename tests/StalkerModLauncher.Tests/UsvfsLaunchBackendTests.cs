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
        var legacyUsvfsLog = Path.Combine(workspace, "userdata", "logs", "usvfs.log");
        Directory.CreateDirectory(Path.GetDirectoryName(legacyUsvfsLog)!);
        File.WriteAllText(legacyUsvfsLog, "legacy diagnostics");
        File.WriteAllText(
            Path.Combine(game, "fsgame.ltx"),
            "$app_data_root$ = true | false | $fs_root$ | _appdata_\\");
        Directory.CreateDirectory(Path.Combine(mod, "bin_x64"));
        CopyExecutable(Path.Combine(mod, "bin_x64", "xrEngine.exe"), WindowsExecutableArchitecture.X64);
        CreateUsvfsRuntimeFiles(game);

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
        Assert.EndsWith(Path.Combine("bin_x64", "xrEngine.exe"), plan.ExecutablePath);
        Assert.DoesNotContain(
            Path.Combine(workspace, "userdata", "usvfs-bootstrap"),
            plan.ExecutablePath,
            StringComparison.OrdinalIgnoreCase);
        Assert.True(FileSystemSafety.IsDirectoryInside(
            plan.ExecutablePath,
            Path.Combine(workspace, ".usvfs-bootstrap")));
        Assert.Equal(
            File.ReadAllBytes(Path.Combine(mod, "bin_x64", "xrEngine.exe")),
            File.ReadAllBytes(plan.ExecutablePath));
        var bootstrapRoot = Path.GetDirectoryName(Path.GetDirectoryName(plan.ExecutablePath))!;
        Assert.Equal(bootstrapRoot, plan.WorkingDirectory);
        Assert.NotNull(plan.RuntimeLease);
        Assert.NotNull(plan.ProcessStarter);
        Assert.NotNull(runtime.MappingPlan);
        Assert.Equal(
            Path.Combine(workspace, "diagnostics", "usvfs.log"),
            runtime.Options?.DiagnosticLogPath);
        Assert.False(File.Exists(legacyUsvfsLog));
        Assert.Equal(
            "legacy diagnostics",
            File.ReadAllText(Path.Combine(workspace, "diagnostics", "usvfs.log")));
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
    public async Task PrepareAsync_BlocksLaunchWhenFsgameCannotIsolateProfileData()
    {
        var game = CreateDirectory("missing-app-data-root-game");
        var workspace = CreateDirectory("missing-app-data-root-workspace");
        File.WriteAllText(Path.Combine(game, "fsgame.ltx"), "$game_data$ = true | true | gamedata\\");
        Directory.CreateDirectory(Path.Combine(game, "bin_x64"));
        CopyExecutable(Path.Combine(game, "bin_x64", "xrEngine.exe"), WindowsExecutableArchitecture.X64);
        CreateUsvfsRuntimeFiles(game);
        var profile = new ModProfile
        {
            Id = "profile-no-app-data-root",
            Name = "No app data root",
            GameInstallPath = game,
            ExecutableRelativePath = @"bin_x64\xrEngine.exe",
            LaunchBackendKind = LaunchBackendKind.VirtualFileSystem
        };
        var layerPlan = FileLayerPlan.CreateLinkedWorkspace(game, profile, workspace);
        var manifest = new OverlayManifestBuilder().BuildLinkedWorkspace(profile, layerPlan, workspace);
        var backend = new UsvfsLaunchBackend(new RecordingUsvfsRuntime(), game);

        var error = await Assert.ThrowsAsync<InvalidDataException>(() => backend.PrepareAsync(
            new ProfileLaunchBackendContext(game, profile, layerPlan, manifest),
            new Progress<string>()));

        Assert.Contains("$app_data_root$", error.Message);
        Assert.Contains("launch was blocked", error.Message);
        Assert.False(File.Exists(Path.Combine(manifest.WriteOverlayRoot, "fsgame.ltx")));
    }

    [Fact]
    public async Task PrepareAsync_SeedsFinalLayerShaderCacheIntoProfileUserData()
    {
        var game = CreateDirectory("shader-cache-game");
        var mod = CreateDirectory("shader-cache-mod");
        var workspace = CreateDirectory("shader-cache-workspace");
        File.WriteAllText(
            Path.Combine(game, "fsgame.ltx"),
            "$app_data_root$ = true | false | $fs_root$ | appdata\\");
        Directory.CreateDirectory(Path.Combine(game, "bin_x64"));
        CopyExecutable(Path.Combine(game, "bin_x64", "xrEngine.exe"), WindowsExecutableArchitecture.X64);
        var baseCache = Path.Combine(game, "appdata", "shaders_cache", "r4", "pp_bloom.ps", "variant");
        var modCache = Path.Combine(mod, "appdata", "shaders_cache", "r4", "pp_bloom.ps", "variant");
        var profileCache = Path.Combine(workspace, "userdata", "shaders_cache", "r4", "pp_bloom.ps", "variant");
        Directory.CreateDirectory(Path.GetDirectoryName(baseCache)!);
        Directory.CreateDirectory(Path.GetDirectoryName(modCache)!);
        Directory.CreateDirectory(Path.GetDirectoryName(profileCache)!);
        File.WriteAllText(baseCache, "base cache");
        File.WriteAllText(modCache, "mod cache");
        File.WriteAllText(profileCache, "stale cache");
        CreateUsvfsRuntimeFiles(game);
        var profile = new ModProfile
        {
            Id = "profile-shader-cache",
            Name = "Shader cache",
            GameInstallPath = game,
            ExecutableRelativePath = @"bin_x64\xrEngine.exe",
            LaunchBackendKind = LaunchBackendKind.VirtualFileSystem
        };
        profile.Mods.Add(new ModEntry
        {
            Id = "shader-cache-mod",
            Name = "Shader cache patch",
            SourcePath = mod,
            IsEnabled = true,
            Order = 1
        });
        var layerPlan = FileLayerPlan.CreateLinkedWorkspace(game, profile, workspace);
        var manifest = new OverlayManifestBuilder().BuildLinkedWorkspace(profile, layerPlan, workspace);
        var backend = new UsvfsLaunchBackend(new RecordingUsvfsRuntime(), game);

        await backend.PrepareAsync(
            new ProfileLaunchBackendContext(game, profile, layerPlan, manifest),
            new Progress<string>());

        Assert.Equal(
            "mod cache",
            File.ReadAllText(profileCache));
        Assert.Equal("base cache", File.ReadAllText(baseCache));
        Assert.Equal("mod cache", File.ReadAllText(modCache));
    }

    [Fact]
    public async Task PrepareAsync_SeedsUserSettingsFromHighestPriorityModAppData()
    {
        var game = CreateDirectory("user-settings-game");
        var mod = CreateDirectory("user-settings-mod");
        var workspace = CreateDirectory("user-settings-workspace");
        File.WriteAllText(
            Path.Combine(game, "fsgame.ltx"),
            "$app_data_root$ = true | false | $fs_root$ | appdata\\");
        Directory.CreateDirectory(Path.Combine(game, "bin_x64"));
        Directory.CreateDirectory(Path.Combine(game, "appdata"));
        Directory.CreateDirectory(Path.Combine(mod, "appdata"));
        CopyExecutable(Path.Combine(game, "bin_x64", "xrEngine.exe"), WindowsExecutableArchitecture.X64);
        File.WriteAllText(Path.Combine(game, "appdata", "user.ltx"), "base settings");
        File.WriteAllText(Path.Combine(mod, "appdata", "user.ltx"), "fix settings");
        CreateUsvfsRuntimeFiles(game);
        var profile = new ModProfile
        {
            Id = "profile-user-settings",
            Name = "User settings",
            GameInstallPath = game,
            ExecutableRelativePath = @"bin_x64\xrEngine.exe",
            LaunchBackendKind = LaunchBackendKind.VirtualFileSystem
        };
        profile.Mods.Add(new ModEntry
        {
            Id = "user-settings-fix",
            Name = "User settings fix",
            SourcePath = mod,
            IsEnabled = true,
            Order = 1
        });
        var layerPlan = FileLayerPlan.CreateLinkedWorkspace(game, profile, workspace);
        var manifest = new OverlayManifestBuilder().BuildLinkedWorkspace(profile, layerPlan, workspace);
        var backend = new UsvfsLaunchBackend(new RecordingUsvfsRuntime(), game);

        await backend.PrepareAsync(
            new ProfileLaunchBackendContext(game, profile, layerPlan, manifest),
            new Progress<string>());

        Assert.Equal(
            "fix settings",
            File.ReadAllText(Path.Combine(workspace, "userdata", "user.ltx")));
        Assert.Equal("base settings", File.ReadAllText(Path.Combine(game, "appdata", "user.ltx")));
        Assert.Equal("fix settings", File.ReadAllText(Path.Combine(mod, "appdata", "user.ltx")));
    }

    [Fact]
    public async Task PrepareAsync_StartsAnomalyLauncherAndKeepsManualEngineOverride()
    {
        var game = CreateDirectory("anomaly-game");
        var mod = CreateDirectory("anomaly-mod");
        var patch = CreateDirectory("anomaly-patch");
        var workspace = CreateDirectory("profile-anomaly-workspace");
        File.WriteAllText(
            Path.Combine(game, "fsgame.ltx"),
            "$app_data_root$ = true | false | $fs_root$ | appdata\\" + Environment.NewLine +
            "$arch_dir_patches$ = false | true | $fs_root$ | patches\\");
        File.WriteAllLines(Path.Combine(game, "AnomalyLauncher.cfg"), ["DX11", "AVX", "1"]);
        File.Copy(
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "SysWOW64", "cmd.exe"),
            Path.Combine(game, "AnomalyLauncher.exe"));
        File.WriteAllText(Path.Combine(game, "commandline.txt"), "-smap2048");
        Directory.CreateDirectory(Path.Combine(game, "bin"));
        Directory.CreateDirectory(Path.Combine(mod, "bin"));
        File.WriteAllText(Path.Combine(game, "bin", "runtime.dll"), "base-runtime");
        File.WriteAllText(Path.Combine(game, "bin", "AnomalyDX9.exe"), "unused-engine");
        var engine = Path.Combine(mod, "bin", "AnomalyDX11AVX.exe");
        File.WriteAllText(engine, string.Empty);
        var dx9AvxEngine = Path.Combine(mod, "bin", "AnomalyDX9AVX.exe");
        CopyExecutable(dx9AvxEngine, WindowsExecutableArchitecture.X64);
        File.WriteAllText(Path.Combine(mod, "bin", "AnomalyDX11.pdb"), "debug symbols");
        File.WriteAllText(Path.Combine(mod, "bin", "feature.dll"), "mod-feature");
        Directory.CreateDirectory(Path.Combine(mod, "db", "mods"));
        File.WriteAllText(Path.Combine(mod, "db", "mods", "engine-data.db0"), "engine data");
        Directory.CreateDirectory(Path.Combine(patch, "bin"));
        var patchEngine = Path.Combine(patch, "bin", "AnomalyDX9AVX.exe");
        File.Copy(
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32", "notepad.exe"),
            patchEngine);
        CreateUsvfsRuntimeFiles(game);

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

        Assert.EndsWith("AnomalyLauncher.exe", plan.ExecutablePath);
        Assert.All(plan.ExecutablePath, character => Assert.True(character <= 0x7F));
        Assert.True(FileSystemSafety.IsDirectoryInside(
            plan.ExecutablePath,
            Path.Combine(workspace, ".usvfs-bootstrap")));
        Assert.DoesNotContain(
            Path.Combine(workspace, "userdata"),
            plan.ExecutablePath,
            StringComparison.OrdinalIgnoreCase);
        Assert.Equal(WindowsExecutableArchitecture.X86, WindowsExecutableArchitectureDetector.Detect(plan.ExecutablePath));
        Assert.Equal("-dbg", plan.Arguments);
        var bootstrapRoot = Path.GetDirectoryName(plan.ExecutablePath)!;
        Assert.Equal(bootstrapRoot, plan.WorkingDirectory);
        Assert.Equal(bootstrapRoot, runtime.MappingPlan?.VirtualRoot);
        Assert.DoesNotContain(
            runtime.MappingPlan!.Operations,
            operation => FileSystemSafety.IsSameDirectory(operation.SourcePath, game));
        Assert.DoesNotContain(
            runtime.MappingPlan.Operations,
            operation => FileSystemSafety.IsSameDirectory(operation.SourcePath, mod));
        Assert.DoesNotContain(
            runtime.MappingPlan.Operations,
            operation => FileSystemSafety.IsSameDirectory(operation.SourcePath, Path.Combine(game, "bin")));
        Assert.DoesNotContain(
            runtime.MappingPlan.Operations,
            operation => FileSystemSafety.IsSameDirectory(operation.SourcePath, Path.Combine(mod, "bin")));
        Assert.Contains(
            runtime.MappingPlan.Operations,
            operation => FileSystemSafety.IsSameDirectory(operation.SourcePath, Path.Combine(mod, "db")));
        Assert.Equal(
            File.ReadAllBytes(Path.Combine(game, "AnomalyLauncher.exe")),
            File.ReadAllBytes(plan.ExecutablePath));
        Assert.Equal(
            File.ReadAllBytes(engine),
            File.ReadAllBytes(Path.Combine(bootstrapRoot, "bin", "AnomalyDX11AVX.exe")));
        Assert.Equal(
            File.ReadAllBytes(patchEngine),
            File.ReadAllBytes(Path.Combine(bootstrapRoot, "bin", "AnomalyDX9AVX.exe")));
        Assert.Equal(
            "base-runtime",
            File.ReadAllText(Path.Combine(bootstrapRoot, "bin", "runtime.dll")));
        Assert.Equal(
            "mod-feature",
            File.ReadAllText(Path.Combine(bootstrapRoot, "bin", "feature.dll")));
        Assert.False(File.Exists(Path.Combine(bootstrapRoot, "bin", "AnomalyDX11.pdb")));

        var bootstrapConfiguration = Path.Combine(bootstrapRoot, "AnomalyLauncher.cfg");
        File.WriteAllText(bootstrapConfiguration, "DX9\nNOAVX\n1");
        Assert.Equal(["DX11", "AVX", "1"], File.ReadAllLines(Path.Combine(game, "AnomalyLauncher.cfg")));

        profile.UsvfsExecutableOverrideRelativePath = @"bin\AnomalyDX9AVX.exe";
        var overridePlan = await backend.PrepareAsync(
            new ProfileLaunchBackendContext(game, profile, layerPlan, manifest),
            new Progress<string>());

        Assert.EndsWith(Path.Combine("bin", "AnomalyDX9AVX.exe"), overridePlan.ExecutablePath);
        Assert.Equal(File.ReadAllBytes(patchEngine), File.ReadAllBytes(overridePlan.ExecutablePath));
        Assert.Equal("base-runtime", File.ReadAllText(Path.Combine(Path.GetDirectoryName(overridePlan.ExecutablePath)!, "runtime.dll")));
        Assert.Equal("mod-feature", File.ReadAllText(Path.Combine(Path.GetDirectoryName(overridePlan.ExecutablePath)!, "feature.dll")));
        Assert.False(File.Exists(Path.Combine(Path.GetDirectoryName(overridePlan.ExecutablePath)!, "AnomalyDX11AVX.exe")));
        Assert.Equal(
            "DX9\nNOAVX\n1",
            File.ReadAllText(Path.Combine(workspace, "userdata", "overwrite", "AnomalyLauncher.cfg")));
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
        CreateUsvfsRuntimeFiles(game);
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
        CreateUsvfsRuntimeFiles(game);
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
        Assert.False(Directory.Exists(Path.Combine(workspace, ".usvfs-bootstrap")));
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
    public async Task PrepareAsync_UsesPhysicalRootForXRayArchiveDirectoriesWithModEngine()
    {
        var game = CreateDirectory("archive-root-game");
        var mod = CreateDirectory("archive-root-mod");
        var workspace = CreateDirectory("archive-root-workspace");
        Directory.CreateDirectory(Path.Combine(game, "patches"));
        Directory.CreateDirectory(Path.Combine(mod, "bin_x64"));
        File.WriteAllText(
            Path.Combine(game, "fsgame.ltx"),
            "$app_data_root$ = true | false | $fs_root$ | appdata\\" + Environment.NewLine +
            "$arch_dir_patches$ = false | true | $fs_root$ | patches\\");
        File.WriteAllText(Path.Combine(game, "patches", "xpatch_02.db"), "game patch");
        File.Copy(
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32", "cmd.exe"),
            Path.Combine(mod, "bin_x64", "xrEngine.exe"));
        File.WriteAllText(Path.Combine(mod, "bin_x64", "engine_patch.dll"), "mod engine");
        CreateUsvfsRuntimeFiles(game);
        var profile = new ModProfile
        {
            Id = "profile-archive-root",
            Name = "Archive root",
            GameInstallPath = game,
            ExecutableRelativePath = @"bin_x64\xrEngine.exe",
            LaunchBackendKind = LaunchBackendKind.VirtualFileSystem
        };
        profile.Mods.Add(new ModEntry
        {
            Id = "archive-engine-mod",
            Name = "Archive engine",
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

        Assert.EndsWith(Path.Combine("bin_x64", "xrEngine.exe"), plan.ExecutablePath);
        Assert.DoesNotContain(
            Path.Combine(workspace, "userdata", "usvfs-bootstrap"),
            plan.ExecutablePath,
            StringComparison.OrdinalIgnoreCase);
        Assert.True(FileSystemSafety.IsDirectoryInside(
            plan.ExecutablePath,
            Path.Combine(workspace, ".usvfs-bootstrap")));
        Assert.Equal(game, plan.WorkingDirectory);
        Assert.Equal(game, runtime.MappingPlan?.VirtualRoot);
        Assert.True(File.Exists(Path.Combine(game, "patches", "xpatch_02.db")));
        Assert.DoesNotContain(
            runtime.MappingPlan!.Operations,
            operation => FileSystemSafety.IsSameDirectory(operation.SourcePath, game));
        Assert.Contains(
            runtime.MappingPlan.Operations,
            operation => FileSystemSafety.IsSameDirectory(operation.SourcePath, mod));
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
        CreateUsvfsRuntimeFiles(game);
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
        CreateUsvfsRuntimeFiles(game);
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

        Assert.EndsWith(Path.Combine("bin_x64", "xrEngine.exe"), plan.ExecutablePath);
        Assert.DoesNotContain(
            Path.Combine(workspace, "userdata", "usvfs-bootstrap"),
            plan.ExecutablePath,
            StringComparison.OrdinalIgnoreCase);
        Assert.True(FileSystemSafety.IsDirectoryInside(
            plan.ExecutablePath,
            Path.Combine(workspace, ".usvfs-bootstrap")));
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
        CreateX64RuntimeFiles(game);
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

    private static void CreateUsvfsRuntimeFiles(string directory)
    {
        CreateX64RuntimeFiles(directory);
        CopyExecutable(
            Path.Combine(directory, UsvfsRuntimeFiles.X86DllFileName),
            WindowsExecutableArchitecture.X86);
        CopyExecutable(
            Path.Combine(directory, UsvfsRuntimeFiles.X86ProxyFileName),
            WindowsExecutableArchitecture.X86);
        CopyExecutable(
            Path.Combine(directory, UsvfsRuntimeFiles.X86HostFileName),
            WindowsExecutableArchitecture.X86);
    }

    private static void CreateX64RuntimeFiles(string directory)
    {
        CopyExecutable(
            Path.Combine(directory, UsvfsRuntimeFiles.ControllerDllFileName),
            WindowsExecutableArchitecture.X64);
        CopyExecutable(
            Path.Combine(directory, UsvfsRuntimeFiles.X64ProxyFileName),
            WindowsExecutableArchitecture.X64);
    }

    private static void CopyExecutable(string destination, WindowsExecutableArchitecture architecture)
    {
        var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var systemDirectory = architecture == WindowsExecutableArchitecture.X86 ? "SysWOW64" : "System32";
        var source = Path.Combine(windows, systemDirectory, "cmd.exe");
        Assert.True(File.Exists(source));
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        File.Copy(source, destination, overwrite: true);
    }

    private sealed class RecordingUsvfsRuntime : IUsvfsRuntime
    {
        public UsvfsMappingPlan? MappingPlan { get; private set; }
        public UsvfsRuntimeOptions? Options { get; private set; }

        public IUsvfsRuntimeSession CreateSession(
            UsvfsMappingPlan mappingPlan,
            UsvfsRuntimeOptions options,
            IProgress<string>? progress = null)
        {
            MappingPlan = mappingPlan;
            Options = options;
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

        public IReadOnlyList<int> GetActiveProcessIds() => [];

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}

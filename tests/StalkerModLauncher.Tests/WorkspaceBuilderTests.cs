using StalkerModLauncher.Models;
using StalkerModLauncher.Services;
using Xunit;

namespace StalkerModLauncher.Tests;

public sealed class WorkspaceBuilderTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "StalkerModLauncherTests",
        Guid.NewGuid().ToString("N"));
    private readonly string _gamePath;
    private readonly string _workspaceRoot;
    private readonly WorkspaceBuilder _builder;

    public WorkspaceBuilderTests()
    {
        _gamePath = Path.Combine(_root, "game");
        _workspaceRoot = Path.Combine(_root, "workspaces");
        _builder = new WorkspaceBuilder(new AppPaths(
            Path.Combine(_root, "config"),
            _workspaceRoot,
            preferGameDriveWorkspace: false));

        CreateFile(_gamePath, "bin/xr_3da.exe", "base executable");
        CreateFile(_gamePath, "fsgame.ltx", "$app_data_root$ = true | false | appdata");
        CreateFile(_gamePath, "gamedata/config/shared.ltx", "base");
        CreateFile(_gamePath, "appdata/user.ltx", "base user settings");
    }

    [Fact]
    public async Task BuildAsync_OverlaysModsInOrderWithoutChangingSources()
    {
        var firstMod = CreateMod("first", "first");
        var secondMod = CreateMod("second", "second");
        var profile = CreateProfile(firstMod, secondMod);

        var result = await _builder.BuildAsync(_gamePath, profile, new ProgressLog());

        Assert.Equal("second", File.ReadAllText(Path.Combine(result.WorkspaceRoot, "gamedata", "config", "shared.ltx")));
        Assert.Equal("base", File.ReadAllText(Path.Combine(_gamePath, "gamedata", "config", "shared.ltx")));
        Assert.Equal("first", File.ReadAllText(Path.Combine(firstMod, "gamedata", "config", "shared.ltx")));
        Assert.Equal("second", File.ReadAllText(Path.Combine(secondMod, "gamedata", "config", "shared.ltx")));
    }

    [Fact]
    public async Task BuildAsync_LinksModConfigurationWithoutCopyingIt()
    {
        var modPath = CreateMod("mod", "mod source");
        var profile = CreateProfile(modPath);

        var result = await _builder.BuildAsync(_gamePath, profile, new ProgressLog());

        Assert.Equal("mod source", File.ReadAllText(Path.Combine(modPath, "gamedata", "config", "shared.ltx")));
        Assert.Equal("base", File.ReadAllText(Path.Combine(_gamePath, "gamedata", "config", "shared.ltx")));
        Assert.True(File.Exists(Path.Combine(result.WorkspaceRoot, "gamedata", "config", "shared.ltx")));
    }

    [Fact]
    public async Task BuildAsync_RewritesOnlyWorkspaceFsgameAndKeepsProfileUserData()
    {
        var modPath = CreateMod("mod", "mod");
        var profile = CreateProfile(modPath);

        var first = await _builder.BuildAsync(_gamePath, profile, new ProgressLog());
        var savePath = Path.Combine(first.ProfileWorkspacePath, "userdata", "savedgames", "test.sav");
        CreateFileAtPath(savePath, "save");
        CreateFileAtPath(Path.Combine(first.ProfileWorkspacePath, "userdata", "user.ltx"), "profile settings");
        CreateFile(modPath, "gamedata/config/new-file.ltx", "changed");

        await _builder.BuildAsync(_gamePath, profile, new ProgressLog());

        Assert.Equal("save", File.ReadAllText(savePath));
        Assert.Equal("profile settings", File.ReadAllText(Path.Combine(first.ProfileWorkspacePath, "userdata", "user.ltx")));
        Assert.Equal("$app_data_root$ = true | false | appdata", File.ReadAllText(Path.Combine(_gamePath, "fsgame.ltx")));
        Assert.Contains(Path.Combine(first.ProfileWorkspacePath, "userdata"), File.ReadAllText(Path.Combine(first.WorkspaceRoot, "fsgame.ltx")));
    }

    [Fact]
    public async Task BuildAsync_UsesCacheUntilOverlayInputChanges()
    {
        var modPath = CreateMod("mod", "first");
        var profile = CreateProfile(modPath);
        await _builder.BuildAsync(_gamePath, profile, new ProgressLog());
        var cachedProgress = new ProgressLog();

        await _builder.BuildAsync(_gamePath, profile, cachedProgress);

        Assert.Contains(cachedProgress.Messages, message => message.Contains("Using cached profile workspace"));

        CreateFile(modPath, "gamedata/config/shared.ltx", "updated content");
        var rebuildProgress = new ProgressLog();
        var rebuilt = await _builder.BuildAsync(_gamePath, profile, rebuildProgress);

        Assert.DoesNotContain(rebuildProgress.Messages, message => message.Contains("Using cached profile workspace"));
        Assert.Equal("updated content", File.ReadAllText(Path.Combine(rebuilt.WorkspaceRoot, "gamedata", "config", "shared.ltx")));
    }

    [Fact]
    public async Task BuildAsync_RebuildsWorkspaceWhenCachedExecutableIsMissing()
    {
        var profile = CreateProfile(CreateMod("mod", "mod"));
        var first = await _builder.BuildAsync(_gamePath, profile, new ProgressLog());
        File.Delete(first.ExecutablePath);
        var progress = new ProgressLog();

        var rebuilt = await _builder.BuildAsync(_gamePath, profile, progress);

        Assert.True(File.Exists(rebuilt.ExecutablePath));
        Assert.Contains(progress.Messages, message => message.Contains("Preparing clean profile workspace"));
    }

    [Fact]
    public async Task BuildAsync_DoesNotFallbackToDedicatedServerExecutable()
    {
        File.Delete(Path.Combine(_gamePath, "bin", "xr_3da.exe"));
        CreateFile(_gamePath, "bin/dedicated/XR_3DA.exe", "dedicated server");
        var profile = CreateProfile();

        var exception = await Assert.ThrowsAsync<FileNotFoundException>(
            () => _builder.BuildAsync(_gamePath, profile, new ProgressLog()));

        Assert.Contains("Profile executable was not found", exception.Message);
    }

    [Fact]
    public void DeleteProfileWorkspace_RejectsUnmanagedDirectoryEvenWithMarker()
    {
        var outside = Path.Combine(_root, "outside");
        CreateFile(outside, ".stalker-launcher-workspace", "fake marker");
        CreateFile(outside, "valuable.txt", "keep");
        var profile = new ModProfile { WorkspacePath = outside };

        Assert.Throws<InvalidOperationException>(() => _builder.DeleteProfileWorkspace(profile, _gamePath));
        Assert.Equal("keep", File.ReadAllText(Path.Combine(outside, "valuable.txt")));
    }

    [Fact]
    public async Task BuildAsync_RejectsManagedWorkspaceRootAsProfileWorkspace()
    {
        var profile = CreateProfile(CreateMod("mod", "mod"));
        profile.WorkspacePath = _workspaceRoot;

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _builder.BuildAsync(_gamePath, profile, new ProgressLog()));

        Assert.Contains("profile-specific folder", exception.Message);
    }

    [Fact]
    public async Task BuildAsync_ReportsMissingEnabledModFolderClearly()
    {
        var missingMod = Path.Combine(_root, "mods", "missing");
        var profile = CreateProfile(missingMod);

        var exception = await Assert.ThrowsAsync<DirectoryNotFoundException>(
            () => _builder.BuildAsync(_gamePath, profile, new ProgressLog()));

        Assert.Contains(missingMod, exception.Message);
        Assert.Equal("base", File.ReadAllText(Path.Combine(_gamePath, "gamedata", "config", "shared.ltx")));
    }

    [Fact]
    public async Task BuildAsync_RejectsEmptyOverlayGamePathBeforeCreatingWorkspace()
    {
        var profile = CreateProfile(CreateMod("mod", "mod"));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _builder.BuildAsync(string.Empty, profile, new ProgressLog()));

        Assert.Contains("выберите папку игры", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(profile.WorkspacePath);
        Assert.False(Directory.Exists(_workspaceRoot));
    }

    [Fact]
    public async Task BuildAsync_CancellationDoesNotChangeSourcesOrWriteManifest()
    {
        var modPath = CreateMod("mod", "mod source");
        var profile = CreateProfile(modPath);
        using var cancellation = new CancellationTokenSource();
        var progress = new ProgressLog(message =>
        {
            if (message.Contains("Preparing clean profile workspace"))
            {
                cancellation.Cancel();
            }
        });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => _builder.BuildAsync(_gamePath, profile, progress, cancellation.Token));

        Assert.Equal("base", File.ReadAllText(Path.Combine(_gamePath, "gamedata", "config", "shared.ltx")));
        Assert.Equal("mod source", File.ReadAllText(Path.Combine(modPath, "gamedata", "config", "shared.ltx")));
        var generatedWorkspace = Path.Combine(_workspaceRoot, $"Test profile-{profile.Id[..8]}");
        Assert.False(File.Exists(Path.Combine(generatedWorkspace, "build-manifest.json")));
    }

    [Fact]
    public async Task BuildAsync_DoesNotMutateBoundProfilePropertiesOnWorkerThread()
    {
        var profile = CreateProfile(CreateMod("mod", "mod"));
        var originalExecutable = profile.ExecutableRelativePath;

        var result = await _builder.BuildAsync(_gamePath, profile, new ProgressLog());

        Assert.Empty(profile.WorkspacePath);
        Assert.Equal(originalExecutable, profile.ExecutableRelativePath);
        Assert.Empty(profile.WorkingDirectoryRelative);
        Assert.EndsWith($"Test profile-{profile.Id[..8]}", result.ProfileWorkspacePath);
    }

    [Fact]
    public async Task BuildAsync_RefreshesUnusedGeneratedWorkspaceAfterProfileRename()
    {
        var profile = CreateProfile(CreateMod("mod", "mod"));
        profile.WorkspacePath = Path.Combine(_workspaceRoot, $"Profile 6-{profile.Id[..8]}");
        profile.Name = "Тень Чернобыля";

        var result = await _builder.BuildAsync(_gamePath, profile, new ProgressLog());

        Assert.EndsWith($"Тень Чернобыля-{profile.Id[..8]}", result.ProfileWorkspacePath);
        Assert.False(Directory.Exists(profile.WorkspacePath));
    }

    [Fact]
    public async Task BuildAsync_UsesManuallyPinnedExecutableSourceOverHigherPriorityMod()
    {
        var mainMod = CreateMod("main", "main");
        var patch = CreateMod("patch", "patch");
        CreateFile(mainMod, "bin_x64/xrEngine.exe", "main executable");
        CreateFile(patch, "bin_x64/xrEngine.exe", "patch executable");
        var profile = CreateProfile(mainMod, patch);
        profile.ExecutableRelativePath = @"bin_x64\xrEngine.exe";
        profile.ExecutableSourcePath = mainMod;

        var result = await _builder.BuildAsync(_gamePath, profile, new ProgressLog());

        Assert.Equal("main executable", File.ReadAllText(result.ExecutablePath));
        Assert.Equal(Path.Combine(result.WorkspaceRoot, "bin_x64", "xrEngine.exe"), result.ExecutablePath);
    }

    private ModProfile CreateProfile(params string[] modPaths)
    {
        var profile = new ModProfile
        {
            Name = "Test profile",
            ExecutableRelativePath = @"bin\xr_3da.exe"
        };

        for (var index = 0; index < modPaths.Length; index++)
        {
            profile.Mods.Add(new ModEntry
            {
                Name = $"Mod {index + 1}",
                SourcePath = modPaths[index],
                IsEnabled = true,
                Order = index + 1
            });
        }

        return profile;
    }

    private string CreateMod(string name, string sharedContent)
    {
        var path = Path.Combine(_root, "mods", name);
        CreateFile(path, "gamedata/config/shared.ltx", sharedContent);
        return path;
    }

    private static void CreateFile(string root, string relativePath, string content)
    {
        CreateFileAtPath(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)), content);
    }

    private static void CreateFileAtPath(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private sealed class ProgressLog(Action<string>? onReport = null) : IProgress<string>
    {
        public List<string> Messages { get; } = [];

        public void Report(string value)
        {
            Messages.Add(value);
            onReport?.Invoke(value);
        }
    }
}

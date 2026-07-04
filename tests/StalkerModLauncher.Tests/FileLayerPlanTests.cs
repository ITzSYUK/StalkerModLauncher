using StalkerModLauncher.Models;
using StalkerModLauncher.Services;
using Xunit;

namespace StalkerModLauncher.Tests;

public sealed class FileLayerPlanTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "StalkerModLauncherLayerPlanTests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public void CreateLinkedWorkspace_OrdersBaseGameEnabledModsAndUserData()
    {
        var gamePath = Path.Combine(Path.GetTempPath(), "game");
        var workspacePath = Path.Combine(Path.GetTempPath(), "workspace");
        var profile = new ModProfile { Name = "Layered profile" };
        var lowerPriorityMod = new ModEntry
        {
            Id = "lower",
            Name = "Main mod",
            SourcePath = Path.Combine(Path.GetTempPath(), "mods", "main"),
            IsEnabled = true,
            Order = 1
        };
        var higherPriorityMod = new ModEntry
        {
            Id = "higher",
            Name = "Patch",
            SourcePath = Path.Combine(Path.GetTempPath(), "mods", "patch"),
            IsEnabled = true,
            Order = 2
        };
        var disabledMod = new ModEntry
        {
            Id = "disabled",
            Name = "Disabled",
            SourcePath = Path.Combine(Path.GetTempPath(), "mods", "disabled"),
            IsEnabled = false,
            Order = 3
        };
        profile.Mods.Add(higherPriorityMod);
        profile.Mods.Add(disabledMod);
        profile.Mods.Add(lowerPriorityMod);

        var plan = FileLayerPlan.CreateLinkedWorkspace(gamePath, profile, workspacePath);

        Assert.Equal(FileLayerKind.BaseGame, plan.Layers[0].Kind);
        Assert.Equal(Path.GetFullPath(gamePath), plan.BaseGame.RootPath);
        Assert.Equal(["lower", "higher"], plan.Mods.Select(layer => layer.Id));
        Assert.Equal([lowerPriorityMod, higherPriorityMod], plan.Mods.Select(layer => layer.Mod));
        Assert.DoesNotContain(plan.Layers, layer => layer.Id == "disabled");
        Assert.Equal(FileLayerKind.UserData, plan.Layers[^1].Kind);
        Assert.Equal(Path.Combine(Path.GetFullPath(workspacePath), "userdata"), plan.UserData.RootPath);
    }

    [Fact]
    public void CreateLinkedWorkspace_RejectsStandaloneProfiles()
    {
        var profile = new ModProfile { IsStandalone = true };

        Assert.Throws<InvalidOperationException>(
            () => FileLayerPlan.CreateLinkedWorkspace("game", profile, "workspace"));
    }

    [Fact]
    public void FindFinalFile_ReturnsHighestPriorityProvider()
    {
        var game = Path.Combine(_root, "game");
        var main = Path.Combine(_root, "main");
        var patch = Path.Combine(_root, "patch");
        CreateFile(game, "gamedata/configs/system.ltx", "base");
        CreateFile(main, "gamedata/configs/system.ltx", "main");
        var patchFile = CreateFile(patch, "gamedata/configs/system.ltx", "patch");
        var profile = CreateProfile(game, main, patch);

        var plan = FileLayerPlan.CreateLinkedWorkspace(game, profile, Path.Combine(_root, "workspace"));
        var providers = plan.FindAllProviders(@"gamedata\configs\system.ltx");
        var final = plan.FindFinalFile(@"gamedata\configs\system.ltx");

        Assert.Equal(3, providers.Count);
        Assert.Equal(patchFile, final?.FullPath);
        Assert.Equal("мод: Patch", final?.SourceName);
    }

    [Fact]
    public void GetOverwrittenFiles_ReturnsFilesReplacedByModLayer()
    {
        var game = Path.Combine(_root, "game-overwrite");
        var main = Path.Combine(_root, "main-overwrite");
        var patch = Path.Combine(_root, "patch-overwrite");
        CreateFile(game, "gamedata/configs/system.ltx", "base");
        var mainFile = CreateFile(main, "gamedata/configs/system.ltx", "main");
        var patchFile = CreateFile(patch, "gamedata/configs/system.ltx", "patch");
        CreateFile(patch, "gamedata/configs/patch_only.ltx", "patch only");
        var profile = CreateProfile(game, main, patch);

        var plan = FileLayerPlan.CreateLinkedWorkspace(game, profile, Path.Combine(_root, "workspace-overwrite"));
        var overwrites = plan.GetOverwrittenFiles(plan.Mods.Single(layer => layer.Name == "Patch"));

        var overwrite = Assert.Single(overwrites);
        Assert.Equal(@"gamedata\configs\system.ltx", overwrite.RelativePath);
        Assert.Equal(mainFile, overwrite.ReplacedFile.FullPath);
        Assert.Equal(patchFile, overwrite.ReplacingFile.FullPath);
    }

    [Fact]
    public void GetExecutableCandidates_ReturnsExecutablesByLayerOrder()
    {
        var game = Path.Combine(_root, "game-exe");
        var main = Path.Combine(_root, "main-exe");
        var patch = Path.Combine(_root, "patch-exe");
        var baseExe = CreateFile(game, "bin/xr_3da.exe", "base");
        var patchExe = CreateFile(patch, "bin_x64/xrEngine.exe", "patch");
        var profile = CreateProfile(game, main, patch);

        var plan = FileLayerPlan.CreateLinkedWorkspace(game, profile, Path.Combine(_root, "workspace-exe"));
        var candidates = plan.GetExecutableCandidates();

        Assert.Equal([baseExe, patchExe], candidates.Select(candidate => candidate.FullPath));
    }

    private ModProfile CreateProfile(string game, string main, string patch)
    {
        var profile = new ModProfile { GameInstallPath = game };
        profile.Mods.Add(new ModEntry { Id = "main", Name = "Main", SourcePath = main, Order = 1 });
        profile.Mods.Add(new ModEntry { Id = "patch", Name = "Patch", SourcePath = patch, Order = 2 });
        return profile;
    }

    private static string CreateFile(string root, string relativePath, string content)
    {
        var path = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}

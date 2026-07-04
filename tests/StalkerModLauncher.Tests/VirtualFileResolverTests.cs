using StalkerModLauncher.Models;
using StalkerModLauncher.Services;
using Xunit;

namespace StalkerModLauncher.Tests;

public sealed class VirtualFileResolverTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "StalkerModLauncherVirtualFileResolverTests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public void ResolveRead_ReturnsWinningLayerFileWhenProfileHasNoOverride()
    {
        var (plan, manifest, patchFile) = CreateLayeredPlan();

        var resolution = new VirtualFileResolver().ResolveRead(
            plan,
            manifest,
            @"gamedata\configs\system.ltx");

        Assert.True(resolution.Exists);
        Assert.Equal(patchFile, resolution.PhysicalPath);
        Assert.Equal(VirtualFileSourceKind.Layer, resolution.SourceKind);
        Assert.Equal("mod-patch", resolution.LayerId);
    }

    [Fact]
    public void ResolveRead_ReturnsOverwriteBeforeModLayers()
    {
        var (plan, manifest, _) = CreateLayeredPlan();
        var overwriteFile = CreateFile(
            manifest.WriteOverlayRoot,
            "gamedata/configs/system.ltx",
            "profile overwrite");

        var resolution = new VirtualFileResolver().ResolveRead(
            plan,
            manifest,
            @"gamedata\configs\system.ltx");

        Assert.Equal(overwriteFile, resolution.PhysicalPath);
        Assert.Equal(VirtualFileSourceKind.Overwrite, resolution.SourceKind);
    }

    [Fact]
    public void ResolveRead_ReturnsKnownWritableFileBeforeModLayers()
    {
        var (plan, manifest, _) = CreateLayeredPlan();
        var writableFile = CreateFile(
            Path.Combine(_root, "workspace"),
            "userdata/writable-game-files/gamedata/configs/localization.ltx",
            "rus");

        var resolution = new VirtualFileResolver().ResolveRead(
            plan,
            manifest,
            Path.Combine("gamedata", "configs", "localization.ltx"));

        Assert.Equal(writableFile, resolution.PhysicalPath);
        Assert.Equal(VirtualFileSourceKind.KnownWritableFile, resolution.SourceKind);
    }

    [Fact]
    public void ResolveWrite_RoutesUnknownFilesToOverwriteArea()
    {
        var (_, manifest, _) = CreateLayeredPlan();

        var resolution = new VirtualFileResolver().ResolveWrite(
            manifest,
            @"gamedata\configs\new_file.ltx");

        Assert.Equal(OverlayWriteTargetKind.DefaultOverwrite, resolution.TargetKind);
        Assert.EndsWith(Path.Combine("userdata", "overwrite", "gamedata", "configs", "new_file.ltx"), resolution.PhysicalPath);
    }

    [Fact]
    public void EnumerateDirectory_MergesLayersAndProfileOverrides()
    {
        var (plan, manifest, _) = CreateLayeredPlan();
        CreateFile(manifest.WriteOverlayRoot, "gamedata/configs/overwrite_only.ltx", "overwrite");
        CreateFile(
            Path.Combine(_root, "workspace"),
            "userdata/writable-game-files/gamedata/configs/localization.ltx",
            "rus");

        var entries = new VirtualFileResolver().EnumerateDirectory(
            plan,
            manifest,
            @"gamedata\configs");

        Assert.Contains(entries, entry => entry.Name == "system.ltx" && entry.LayerId == "mod-patch");
        Assert.Contains(entries, entry => entry.Name == "overwrite_only.ltx" && entry.SourceKind == VirtualFileSourceKind.Overwrite);
        Assert.Contains(entries, entry => entry.Name == "localization.ltx" && entry.SourceKind == VirtualFileSourceKind.KnownWritableFile);
    }

    private (FileLayerPlan Plan, OverlayManifest Manifest, string PatchFile) CreateLayeredPlan()
    {
        var game = Path.Combine(_root, "game");
        var mod = Path.Combine(_root, "mod");
        var patch = Path.Combine(_root, "patch");
        var workspace = Path.Combine(_root, "workspace");
        CreateFile(game, "gamedata/configs/system.ltx", "base");
        CreateFile(mod, "gamedata/configs/system.ltx", "mod");
        var patchFile = CreateFile(patch, "gamedata/configs/system.ltx", "patch");
        var profile = new ModProfile { GameInstallPath = game };
        profile.Mods.Add(new ModEntry { Id = "mod-main", Name = "Main", SourcePath = mod, Order = 1 });
        profile.Mods.Add(new ModEntry { Id = "mod-patch", Name = "Patch", SourcePath = patch, Order = 2 });

        var plan = FileLayerPlan.CreateLinkedWorkspace(game, profile, workspace);
        var manifest = new OverlayManifestBuilder().BuildLinkedWorkspace(profile, plan, workspace);
        return (plan, manifest, patchFile);
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

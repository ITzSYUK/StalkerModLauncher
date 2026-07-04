using StalkerModLauncher.Models;
using StalkerModLauncher.Services;
using Xunit;

namespace StalkerModLauncher.Tests;

public sealed class OverlayDiagnosticsServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "StalkerModLauncherOverlayDiagnosticsTests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public void InspectFile_ReturnsVisibleProviderAndAllProviders()
    {
        var game = Path.Combine(_root, "game");
        var mod = Path.Combine(_root, "mod");
        var patch = Path.Combine(_root, "patch");
        var workspace = Path.Combine(_root, "workspace");
        CreateFile(game, "gamedata/configs/system.ltx", "base");
        CreateFile(mod, "gamedata/configs/system.ltx", "mod");
        var patchFile = CreateFile(patch, "gamedata/configs/system.ltx", "patch");
        var profile = CreateProfile(game, mod, patch);
        var plan = FileLayerPlan.CreateLinkedWorkspace(game, profile, workspace);
        var manifest = new OverlayManifestBuilder().BuildLinkedWorkspace(profile, plan, workspace);

        var diagnostic = new OverlayDiagnosticsService().InspectFile(
            plan,
            manifest,
            @"gamedata\configs\system.ltx");

        Assert.True(diagnostic.Exists);
        Assert.Equal(3, diagnostic.Providers.Count);
        Assert.Equal(patchFile, diagnostic.VisibleFile?.FullPath);
        Assert.Equal(OverlayWriteTargetKind.DefaultOverwrite, diagnostic.WriteTarget.Kind);
        Assert.EndsWith(Path.Combine("userdata", "overwrite", "gamedata", "configs", "system.ltx"), diagnostic.WriteTarget.StoragePath);
    }

    [Fact]
    public void InspectFile_UsesKnownWritableTargetForLanguageSettings()
    {
        var game = Path.Combine(_root, "game-language");
        var mod = Path.Combine(_root, "mod-language");
        var workspace = Path.Combine(_root, "workspace-language");
        CreateFile(game, "fsgame.ltx", "base");
        var profile = CreateProfile(game, mod);
        var plan = FileLayerPlan.CreateLinkedWorkspace(game, profile, workspace);
        var manifest = new OverlayManifestBuilder().BuildLinkedWorkspace(profile, plan, workspace);

        var diagnostic = new OverlayDiagnosticsService().InspectFile(
            plan,
            manifest,
            Path.Combine("gamedata", "configs", "localization.ltx"));

        Assert.False(diagnostic.Exists);
        Assert.Equal(OverlayWriteTargetKind.KnownWritableFile, diagnostic.WriteTarget.Kind);
        Assert.EndsWith(
            Path.Combine("userdata", "writable-game-files", "gamedata", "configs", "localization.ltx"),
            diagnostic.WriteTarget.StoragePath);
    }

    private static ModProfile CreateProfile(string game, params string[] mods)
    {
        var profile = new ModProfile { GameInstallPath = game };
        for (var index = 0; index < mods.Length; index++)
        {
            profile.Mods.Add(new ModEntry
            {
                Id = $"mod-{index}",
                Name = $"Mod {index + 1}",
                SourcePath = mods[index],
                Order = index + 1
            });
        }

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

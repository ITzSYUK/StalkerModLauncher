using StalkerModLauncher.Models;
using StalkerModLauncher.Services;
using Xunit;

namespace StalkerModLauncher.Tests;

public sealed class VirtualWriteCaptureServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "StalkerModLauncherVirtualWriteCaptureTests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public void PrepareWriteTarget_CreatesDirectoryInsideOverwriteArea()
    {
        var (_, manifest) = CreatePlanAndManifest();
        var resolution = new VirtualFileResolver().ResolveWrite(
            manifest,
            @"gamedata\config\new_file.ltx");

        var path = new VirtualWriteCaptureService().PrepareWriteTarget(manifest, resolution);

        Assert.EndsWith(Path.Combine("userdata", "overwrite", "gamedata", "config", "new_file.ltx"), path);
        Assert.True(Directory.Exists(Path.GetDirectoryName(path)));
    }

    [Fact]
    public void PrepareWriteTarget_AllowsKnownWritableFile()
    {
        var (_, manifest) = CreatePlanAndManifest();
        var resolution = new VirtualFileResolver().ResolveWrite(
            manifest,
            Path.Combine("gamedata", "configs", "localization.ltx"));

        var path = new VirtualWriteCaptureService().PrepareWriteTarget(manifest, resolution);

        Assert.EndsWith(
            Path.Combine("userdata", "writable-game-files", "gamedata", "configs", "localization.ltx"),
            path);
        Assert.True(Directory.Exists(Path.GetDirectoryName(path)));
    }

    [Fact]
    public void PrepareWriteTarget_RejectsPathOutsideProfileWritableArea()
    {
        var (_, manifest) = CreatePlanAndManifest();
        var resolution = new VirtualWriteResolution(
            "evil.ltx",
            Path.Combine(_root, "game", "evil.ltx"),
            OverlayWriteTargetKind.DefaultOverwrite,
            "test");

        Assert.Throws<InvalidOperationException>(() =>
            new VirtualWriteCaptureService().PrepareWriteTarget(manifest, resolution));
    }

    private (FileLayerPlan Plan, OverlayManifest Manifest) CreatePlanAndManifest()
    {
        var game = Path.Combine(_root, "game");
        var workspace = Path.Combine(_root, "workspace");
        CreateFile(game, "fsgame.ltx", "base");
        var profile = new ModProfile { GameInstallPath = game };
        var plan = FileLayerPlan.CreateLinkedWorkspace(game, profile, workspace);
        var manifest = new OverlayManifestBuilder().BuildLinkedWorkspace(profile, plan, workspace);
        return (plan, manifest);
    }

    private static void CreateFile(string root, string relativePath, string content)
    {
        var path = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
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
}

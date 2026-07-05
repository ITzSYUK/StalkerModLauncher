using StalkerModLauncher.Models;
using StalkerModLauncher.Services;
using Xunit;

namespace StalkerModLauncher.Tests;

public sealed class UsvfsMappingPlanBuilderTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "StalkerModLauncherUsvfsMappingTests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public void Build_MapsBaseGameAndModsToBaseGameVirtualRootInPriorityOrder()
    {
        var game = CreateDirectory("game");
        var firstMod = CreateDirectory("mod1");
        var patch = CreateDirectory("mod2");
        var workspace = CreateDirectory("workspace");
        var profile = new ModProfile
        {
            Name = "Layered",
            GameInstallPath = game
        };
        profile.Mods.Add(new ModEntry
        {
            Id = "mod1",
            Name = "Main mod",
            SourcePath = firstMod,
            IsEnabled = true,
            Order = 1
        });
        profile.Mods.Add(new ModEntry
        {
            Id = "mod2",
            Name = "Patch",
            SourcePath = patch,
            IsEnabled = true,
            Order = 2
        });
        var layerPlan = FileLayerPlan.CreateLinkedWorkspace(game, profile, workspace);
        var manifest = new OverlayManifestBuilder().BuildLinkedWorkspace(profile, layerPlan, workspace);
        var builder = new UsvfsMappingPlanBuilder();

        var plan = builder.Build(layerPlan, manifest);

        Assert.Equal(Path.GetFullPath(game), plan.VirtualRoot);
        Assert.Equal(
            [Path.GetFullPath(game), Path.GetFullPath(firstMod), Path.GetFullPath(patch)],
            plan.Operations
                .Where(operation => operation.SourceName != "profile overwrite")
                .Select(operation => operation.SourcePath)
                .ToArray());
        Assert.All(
            plan.Operations.Where(operation => operation.SourceName != "profile overwrite"),
            operation => Assert.Equal(Path.GetFullPath(game), operation.DestinationPath));
    }

    [Fact]
    public void Build_AddsProfileOverwriteAsCreateTargetAtHighestPriority()
    {
        var game = CreateDirectory("game");
        var workspace = CreateDirectory("workspace");
        var profile = new ModProfile
        {
            Name = "Layered",
            GameInstallPath = game
        };
        var layerPlan = FileLayerPlan.CreateLinkedWorkspace(game, profile, workspace);
        var manifest = new OverlayManifestBuilder().BuildLinkedWorkspace(profile, layerPlan, workspace);
        var builder = new UsvfsMappingPlanBuilder();

        var plan = builder.Build(layerPlan, manifest);

        var overwrite = Assert.Single(plan.Operations, operation => operation.SourceName == "profile overwrite");
        Assert.Equal(UsvfsMappingKind.DirectoryStatic, overwrite.Kind);
        Assert.Equal(Path.GetFullPath(manifest.WriteOverlayRoot), overwrite.SourcePath);
        Assert.Equal(Path.GetFullPath(game), overwrite.DestinationPath);
        Assert.True(overwrite.MonitorChanges);
        Assert.True(overwrite.CreateTarget);
    }

    [Fact]
    public void Build_AddsExistingKnownWritableFilesBeforeOverwriteCreateTarget()
    {
        var game = CreateDirectory("game");
        var workspace = CreateDirectory("workspace");
        var profile = new ModProfile
        {
            Name = "Layered",
            GameInstallPath = game
        };
        var layerPlan = FileLayerPlan.CreateLinkedWorkspace(game, profile, workspace);
        var manifest = new OverlayManifestBuilder().BuildLinkedWorkspace(profile, layerPlan, workspace);
        var writableFile = manifest.WritableFiles.Single(file => file.RelativePath == "fsgame.ltx");
        Directory.CreateDirectory(Path.GetDirectoryName(writableFile.StoragePath)!);
        File.WriteAllText(writableFile.StoragePath, "profile fsgame");
        var builder = new UsvfsMappingPlanBuilder();

        var plan = builder.Build(layerPlan, manifest);

        var knownWritable = Assert.Single(
            plan.Operations,
            operation => operation.Kind == UsvfsMappingKind.File && operation.SourceName == "profile writable files");
        Assert.Equal(Path.GetFullPath(writableFile.StoragePath), knownWritable.SourcePath);
        Assert.Equal(Path.Combine(Path.GetFullPath(game), "fsgame.ltx"), knownWritable.DestinationPath);
        Assert.True(knownWritable.Order < plan.Operations.Single(operation => operation.SourceName == "profile overwrite").Order);
    }

    public void Dispose()
    {
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
}

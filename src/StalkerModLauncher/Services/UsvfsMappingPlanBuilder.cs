using StalkerModLauncher.Models;

namespace StalkerModLauncher.Services;

public sealed class UsvfsMappingPlanBuilder
{
    public UsvfsMappingPlan Build(
        FileLayerPlan layerPlan,
        OverlayManifest manifest,
        string? virtualRootOverride = null)
    {
        var baseGameRoot = Path.GetFullPath(layerPlan.BaseGame.RootPath);
        var virtualRoot = Path.GetFullPath(virtualRootOverride ?? baseGameRoot);
        var operations = new List<UsvfsMappingOperation>();

        // Never map the game directory onto itself: USVFS would hide physical files
        // such as gamedata.db*. A separate bootstrap root does need the base layer.
        if (!FileSystemSafety.IsSameDirectory(baseGameRoot, virtualRoot))
        {
            operations.Add(new UsvfsMappingOperation(
                UsvfsMappingKind.DirectoryStatic,
                baseGameRoot,
                virtualRoot,
                FileLayerPlan.GetDisplayName(layerPlan.BaseGame),
                layerPlan.BaseGame.Order,
                MonitorChanges: false,
                CreateTarget: false));
        }

        foreach (var layer in layerPlan.Mods)
        {
            operations.Add(new UsvfsMappingOperation(
                UsvfsMappingKind.DirectoryStatic,
                Path.GetFullPath(layer.RootPath),
                virtualRoot,
                FileLayerPlan.GetDisplayName(layer),
                layer.Order,
                MonitorChanges: false,
                CreateTarget: false));
        }

        AddWritableFiles(operations, virtualRoot, manifest);

        operations.Add(new UsvfsMappingOperation(
            UsvfsMappingKind.DirectoryStatic,
            Path.GetFullPath(manifest.WriteOverlayRoot),
            virtualRoot,
            "profile overwrite",
            int.MaxValue,
            MonitorChanges: true,
            CreateTarget: true));

        return new UsvfsMappingPlan(
            virtualRoot,
            Path.GetFullPath(manifest.WriteOverlayRoot),
            operations);
    }

    private static void AddWritableFiles(
        ICollection<UsvfsMappingOperation> operations,
        string virtualRoot,
        OverlayManifest manifest)
    {
        foreach (var writableFile in manifest.WritableFiles.Where(file => File.Exists(file.StoragePath)))
        {
            FileSystemSafety.EnsureRelativePath(writableFile.RelativePath, "USVFS writable file");
            operations.Add(new UsvfsMappingOperation(
                UsvfsMappingKind.File,
                Path.GetFullPath(writableFile.StoragePath),
                Path.Combine(virtualRoot, writableFile.RelativePath),
                "profile writable files",
                int.MaxValue - 1,
                MonitorChanges: false,
                CreateTarget: false));
        }
    }
}

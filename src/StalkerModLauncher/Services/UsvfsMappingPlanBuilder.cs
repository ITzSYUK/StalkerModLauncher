using StalkerModLauncher.Models;

namespace StalkerModLauncher.Services;

public sealed class UsvfsMappingPlanBuilder
{
    public UsvfsMappingPlan Build(FileLayerPlan layerPlan, OverlayManifest manifest)
    {
        var virtualRoot = Path.GetFullPath(layerPlan.BaseGame.RootPath);
        var operations = new List<UsvfsMappingOperation>();

        foreach (var layer in layerPlan.SourceLayers)
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

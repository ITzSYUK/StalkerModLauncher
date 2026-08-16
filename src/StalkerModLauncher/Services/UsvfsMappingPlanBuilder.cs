using StalkerModLauncher.Models;

namespace StalkerModLauncher.Services;

public static class UsvfsMappingPlanBuilder
{
    public static UsvfsMappingPlan Build(
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

        AddExcludedFileRestorations(operations, layerPlan, virtualRoot);
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

    public static UsvfsMappingPlan BuildAnomalyLauncherBootstrap(
        FileLayerPlan layerPlan,
        OverlayManifest manifest,
        string virtualRoot)
    {
        var fullVirtualRoot = Path.GetFullPath(virtualRoot);
        var operations = new List<UsvfsMappingOperation>();

        AddLayerDirectoriesAroundPhysicalBin(
            operations,
            layerPlan.BaseGame,
            fullVirtualRoot);
        foreach (var layer in layerPlan.Mods)
        {
            AddLayerDirectoriesAroundPhysicalBin(
                operations,
                layer,
                fullVirtualRoot);
        }

        AddExcludedFileRestorations(operations, layerPlan, fullVirtualRoot);
        AddWritableFiles(operations, fullVirtualRoot, manifest);
        operations.Add(new UsvfsMappingOperation(
            UsvfsMappingKind.DirectoryStatic,
            Path.GetFullPath(manifest.WriteOverlayRoot),
            fullVirtualRoot,
            "profile overwrite",
            int.MaxValue,
            MonitorChanges: true,
            CreateTarget: true));

        return new UsvfsMappingPlan(
            fullVirtualRoot,
            Path.GetFullPath(manifest.WriteOverlayRoot),
            operations);
    }

    private static void AddLayerDirectoriesAroundPhysicalBin(
        List<UsvfsMappingOperation> operations,
        FileLayer layer,
        string virtualRoot)
    {
        var sourceRoot = Path.GetFullPath(layer.RootPath);
        if (!Directory.Exists(sourceRoot))
        {
            return;
        }

        var sourceName = FileLayerPlan.GetDisplayName(layer);
        foreach (var sourceDirectory in Directory.EnumerateDirectories(sourceRoot, "*", SearchOption.TopDirectoryOnly))
        {
            var directoryName = Path.GetFileName(sourceDirectory);
            if (!directoryName.Equals("bin", StringComparison.OrdinalIgnoreCase))
            {
                operations.Add(new UsvfsMappingOperation(
                    UsvfsMappingKind.DirectoryStatic,
                    sourceDirectory,
                    Path.Combine(virtualRoot, directoryName),
                    sourceName,
                    layer.Order,
                    MonitorChanges: false,
                    CreateTarget: false));
                continue;
            }

            foreach (var binSubdirectory in Directory.EnumerateDirectories(
                         sourceDirectory,
                         "*",
                         SearchOption.TopDirectoryOnly))
            {
                operations.Add(new UsvfsMappingOperation(
                    UsvfsMappingKind.DirectoryStatic,
                    binSubdirectory,
                    Path.Combine(virtualRoot, "bin", Path.GetFileName(binSubdirectory)),
                    sourceName,
                    layer.Order,
                    MonitorChanges: false,
                    CreateTarget: false));
            }
        }
    }

    private static void AddWritableFiles(
        List<UsvfsMappingOperation> operations,
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

    private static void AddExcludedFileRestorations(
        List<UsvfsMappingOperation> operations,
        FileLayerPlan layerPlan,
        string virtualRoot)
    {
        var excludedPaths = layerPlan.Mods
            .SelectMany(layer => layer.Mod?.ExcludedFiles ?? [])
            .Distinct(StringComparer.OrdinalIgnoreCase);
        foreach (var relativePath in excludedPaths)
        {
            FileSystemSafety.EnsureRelativePath(relativePath, "Excluded mod file");
            var provider = layerPlan.FindFinalFile(relativePath);
            if (provider is null)
            {
                continue;
            }

            operations.Add(new UsvfsMappingOperation(
                UsvfsMappingKind.File,
                Path.GetFullPath(provider.FullPath),
                Path.Combine(virtualRoot, relativePath),
                $"excluded file fallback: {provider.SourceName}",
                int.MaxValue - 2,
                MonitorChanges: false,
                CreateTarget: false));
        }
    }
}

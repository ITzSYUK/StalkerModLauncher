using StalkerModLauncher.Models;

namespace StalkerModLauncher.Services;

public sealed class OverlayDiagnosticsService
{
    public OverlayFileDiagnostic InspectFile(
        FileLayerPlan layerPlan,
        OverlayManifest manifest,
        string relativePath)
    {
        FileSystemSafety.EnsureRelativePath(relativePath, "Overlay file");
        var normalizedRelativePath = NormalizeRelativePath(relativePath);
        var providers = layerPlan
            .FindAllProviders(normalizedRelativePath)
            .Select(ToSnapshot)
            .ToArray();

        return new OverlayFileDiagnostic(
            normalizedRelativePath,
            providers.LastOrDefault(),
            providers,
            ResolveWriteTarget(manifest, normalizedRelativePath));
    }

    public OverlayWriteTargetSnapshot ResolveWriteTarget(OverlayManifest manifest, string relativePath)
    {
        FileSystemSafety.EnsureRelativePath(relativePath, "Overlay write target");
        var normalizedRelativePath = NormalizeRelativePath(relativePath);
        var knownWritable = manifest.WritableFiles.FirstOrDefault(file =>
            PathEquals(file.RelativePath, normalizedRelativePath));

        if (knownWritable is not null)
        {
            return new OverlayWriteTargetSnapshot(
                OverlayWriteTargetKind.KnownWritableFile,
                normalizedRelativePath,
                knownWritable.StoragePath,
                knownWritable.Reason);
        }

        return new OverlayWriteTargetSnapshot(
            OverlayWriteTargetKind.DefaultOverwrite,
            normalizedRelativePath,
            Path.Combine(manifest.WriteOverlayRoot, normalizedRelativePath),
            "Future VFS writes for unknown game files should go to the profile overwrite area.");
    }

    private static OverlayFileProviderSnapshot ToSnapshot(FileLayerFile file)
    {
        return new OverlayFileProviderSnapshot(
            file.Layer.Kind,
            file.Layer.Id,
            file.Layer.Name,
            file.FullPath,
            NormalizeRelativePath(file.RelativePath),
            file.SourceName,
            file.Layer.Order);
    }

    private static bool PathEquals(string left, string right)
    {
        return string.Equals(
            NormalizeRelativePath(left),
            NormalizeRelativePath(right),
            StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeRelativePath(string relativePath)
    {
        return relativePath.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
    }
}

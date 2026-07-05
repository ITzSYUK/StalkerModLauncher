using StalkerModLauncher.Models;

namespace StalkerModLauncher.Services;

public sealed class VirtualWriteCaptureService
{
    public string PrepareWriteTarget(OverlayManifest manifest, VirtualWriteResolution resolution)
    {
        if (!IsAllowedWriteTarget(manifest, resolution.PhysicalPath))
        {
            throw new InvalidOperationException("VFS write target must stay inside the profile writable area.");
        }

        var directory = Path.GetDirectoryName(resolution.PhysicalPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        return resolution.PhysicalPath;
    }

    private static bool IsAllowedWriteTarget(OverlayManifest manifest, string physicalPath)
    {
        var fullPath = Path.GetFullPath(physicalPath);
        if (FileSystemSafety.IsDirectoryInside(fullPath, manifest.WriteOverlayRoot))
        {
            return true;
        }

        return manifest.WritableFiles.Any(file =>
            string.Equals(Path.GetFullPath(file.StoragePath), fullPath, StringComparison.OrdinalIgnoreCase));
    }
}

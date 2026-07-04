using StalkerModLauncher.Models;

namespace StalkerModLauncher.Services;

public sealed class VirtualFileSystemLaunchBackend : IProfileLaunchBackend
{
    public LaunchBackendKind Kind => LaunchBackendKind.VirtualFileSystem;

    public Task<LaunchPlan> PrepareAsync(
        ProfileLaunchBackendContext context,
        IProgress<string> progress,
        CancellationToken cancellationToken = default)
    {
        var layerCount = context.OverlayManifest?.Layers.Count ?? context.FileLayerPlan?.Layers.Count ?? 0;
        progress.Report($"Virtual file system launch backend is reserved for future USVFS-style integration. Overlay layers: {layerCount:N0}.");
        throw new NotSupportedException("Virtual file system launch mode is not implemented yet. Use the workspace launch mode.");
    }
}

using System.Diagnostics;
using StalkerModLauncher.Models;

namespace StalkerModLauncher.Services;

public sealed class VirtualFileSystemLaunchBackend : IProfileLaunchBackend
{
    public LaunchBackendKind Kind => LaunchBackendKind.VirtualFileSystem;

    public Task<Process> LaunchAsync(
        string gamePath,
        ModProfile profile,
        IProgress<string> progress,
        CancellationToken cancellationToken = default)
    {
        progress.Report("Virtual file system launch backend is reserved for future USVFS-style integration.");
        throw new NotSupportedException("Virtual file system launch mode is not implemented yet. Use the workspace launch mode.");
    }
}

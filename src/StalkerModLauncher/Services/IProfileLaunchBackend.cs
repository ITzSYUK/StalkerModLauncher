using System.Diagnostics;
using StalkerModLauncher.Models;

namespace StalkerModLauncher.Services;

public interface IProfileLaunchBackend
{
    LaunchBackendKind Kind { get; }

    Task<Process> LaunchAsync(
        string gamePath,
        ModProfile profile,
        IProgress<string> progress,
        CancellationToken cancellationToken = default);
}

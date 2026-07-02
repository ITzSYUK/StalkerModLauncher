using StalkerModLauncher.Models;

namespace StalkerModLauncher.Services;

public interface IProfileLaunchBackend
{
    LaunchBackendKind Kind { get; }

    Task<LaunchPlan> PrepareAsync(
        string gamePath,
        ModProfile profile,
        IProgress<string> progress,
        CancellationToken cancellationToken = default);
}

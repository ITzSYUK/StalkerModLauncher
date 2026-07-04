using StalkerModLauncher.Models;

namespace StalkerModLauncher.Services;

public interface IProfileLaunchBackend
{
    LaunchBackendKind Kind { get; }

    Task<LaunchPlan> PrepareAsync(
        ProfileLaunchBackendContext context,
        IProgress<string> progress,
        CancellationToken cancellationToken = default);
}

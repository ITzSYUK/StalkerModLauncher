using StalkerModLauncher.Models;

namespace StalkerModLauncher.Services;

public interface IUsvfsRuntime
{
    Task<UsvfsProcessLaunchResult> RunAsync(
        UsvfsMappingPlan mappingPlan,
        UsvfsProcessLaunchRequest launchRequest,
        UsvfsRuntimeOptions options,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default);
}

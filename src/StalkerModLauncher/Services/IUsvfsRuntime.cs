using StalkerModLauncher.Models;

namespace StalkerModLauncher.Services;

public interface IUsvfsRuntime
{
    IUsvfsRuntimeSession CreateSession(
        UsvfsMappingPlan mappingPlan,
        UsvfsRuntimeOptions options,
        IProgress<string>? progress = null);

    Task<UsvfsProcessLaunchResult> RunAsync(
        UsvfsMappingPlan mappingPlan,
        UsvfsProcessLaunchRequest launchRequest,
        UsvfsRuntimeOptions options,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default);
}

public interface IUsvfsRuntimeSession : IAsyncDisposable
{
    System.Diagnostics.Process StartProcess(
        UsvfsProcessLaunchRequest launchRequest,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default);

    Task<int> GetExitCodeAsync(CancellationToken cancellationToken = default);
}

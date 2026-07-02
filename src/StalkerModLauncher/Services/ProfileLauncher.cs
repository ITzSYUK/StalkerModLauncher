using System.Diagnostics;
using StalkerModLauncher.Models;

namespace StalkerModLauncher.Services;

public interface IProfileLauncher
{
    Task<Process> LaunchAsync(
        string gamePath,
        ModProfile profile,
        IProgress<string> progress,
        CancellationToken cancellationToken = default);
}

public sealed class ProfileLauncher : IProfileLauncher
{
    private readonly IReadOnlyDictionary<LaunchBackendKind, IProfileLaunchBackend> _backends;
    private readonly ILaunchPlanExecutor _launchPlanExecutor;

    public ProfileLauncher(IEnumerable<IProfileLaunchBackend> backends, ILaunchPlanExecutor? launchPlanExecutor = null)
    {
        _backends = backends.ToDictionary(backend => backend.Kind);
        if (!_backends.ContainsKey(LaunchBackendKind.LinkedWorkspace))
        {
            throw new ArgumentException("The linked workspace launch backend must be registered.", nameof(backends));
        }

        _launchPlanExecutor = launchPlanExecutor ?? new LaunchPlanExecutor();
    }

    public async Task<Process> LaunchAsync(
        string gamePath,
        ModProfile profile,
        IProgress<string> progress,
        CancellationToken cancellationToken = default)
    {
        var backend = ResolveBackend(profile.LaunchBackendKind);
        progress.Report($"Launch backend: {backend.Kind}.");
        var plan = await backend.PrepareAsync(gamePath, profile, progress, cancellationToken);
        progress.Report($"Starting: {plan.ExecutablePath}");
        return _launchPlanExecutor.Start(plan);
    }

    private IProfileLaunchBackend ResolveBackend(LaunchBackendKind kind)
    {
        if (_backends.TryGetValue(kind, out var backend))
        {
            return backend;
        }

        if (kind == LaunchBackendKind.VirtualFileSystem)
        {
            throw new NotSupportedException("Virtual file system launch mode is not implemented yet. Use the workspace launch mode.");
        }

        return _backends[LaunchBackendKind.LinkedWorkspace];
    }
}

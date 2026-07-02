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

    public ProfileLauncher(IEnumerable<IProfileLaunchBackend> backends)
    {
        _backends = backends.ToDictionary(backend => backend.Kind);
        if (!_backends.ContainsKey(LaunchBackendKind.LinkedWorkspace))
        {
            throw new ArgumentException("The linked workspace launch backend must be registered.", nameof(backends));
        }
    }

    public Task<Process> LaunchAsync(
        string gamePath,
        ModProfile profile,
        IProgress<string> progress,
        CancellationToken cancellationToken = default)
    {
        var backend = ResolveBackend(profile.LaunchBackendKind);
        progress.Report($"Launch backend: {backend.Kind}.");
        return backend.LaunchAsync(gamePath, profile, progress, cancellationToken);
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

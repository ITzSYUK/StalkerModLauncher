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
    private readonly ProfileManager? _profileManager;
    private readonly bool _allowExperimentalVirtualFileSystem;
    private readonly OverlayManifestBuilder _overlayManifestBuilder = new();

    public ProfileLauncher(
        IEnumerable<IProfileLaunchBackend> backends,
        ILaunchPlanExecutor? launchPlanExecutor = null,
        ProfileManager? profileManager = null,
        bool allowExperimentalVirtualFileSystem = false)
    {
        _backends = backends.ToDictionary(backend => backend.Kind);
        if (!_backends.ContainsKey(LaunchBackendKind.LinkedWorkspace))
        {
            throw new ArgumentException("The linked workspace launch backend must be registered.", nameof(backends));
        }

        _launchPlanExecutor = launchPlanExecutor ?? new LaunchPlanExecutor();
        _profileManager = profileManager;
        _allowExperimentalVirtualFileSystem = allowExperimentalVirtualFileSystem;
    }

    public async Task<Process> LaunchAsync(
        string gamePath,
        ModProfile profile,
        IProgress<string> progress,
        CancellationToken cancellationToken = default)
    {
        var backend = ResolveBackend(profile.LaunchBackendKind);
        progress.Report($"Launch backend: {backend.Kind}.");
        var context = CreateBackendContext(gamePath, profile);
        var plan = await backend.PrepareAsync(context, progress, cancellationToken);
        progress.Report($"Starting: {plan.ExecutablePath}");
        return _launchPlanExecutor.Start(plan);
    }

    private ProfileLaunchBackendContext CreateBackendContext(string gamePath, ModProfile profile)
    {
        if (profile.IsStandalone || _profileManager is null || string.IsNullOrWhiteSpace(gamePath))
        {
            return new ProfileLaunchBackendContext(gamePath, profile);
        }

        var workspace = _profileManager.GetProfileFolderPath(profile);
        if (string.IsNullOrWhiteSpace(workspace))
        {
            return new ProfileLaunchBackendContext(gamePath, profile);
        }

        var fileLayerPlan = FileLayerPlan.CreateLinkedWorkspace(gamePath, profile, workspace);
        var overlayManifest = _overlayManifestBuilder.BuildLinkedWorkspace(profile, fileLayerPlan, workspace);
        return new ProfileLaunchBackendContext(gamePath, profile, fileLayerPlan, overlayManifest);
    }

    private IProfileLaunchBackend ResolveBackend(LaunchBackendKind kind)
    {
        if (kind == LaunchBackendKind.VirtualFileSystem && !_allowExperimentalVirtualFileSystem)
        {
            throw new NotSupportedException(
                "Virtual file system launch mode is experimental and disabled. Use the linked workspace launch mode.");
        }

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

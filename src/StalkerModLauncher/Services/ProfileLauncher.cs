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
    private readonly OverlayManifestBuilder _overlayManifestBuilder = new();

    public ProfileLauncher(
        IEnumerable<IProfileLaunchBackend> backends,
        ILaunchPlanExecutor? launchPlanExecutor = null,
        ProfileManager? profileManager = null)
    {
        _backends = backends.ToDictionary(backend => backend.Kind);
        if (!_backends.ContainsKey(LaunchBackendKind.LinkedWorkspace))
        {
            throw new ArgumentException("The linked workspace launch backend must be registered.", nameof(backends));
        }

        _launchPlanExecutor = launchPlanExecutor ?? new LaunchPlanExecutor();
        _profileManager = profileManager;
    }

    public async Task<Process> LaunchAsync(
        string gamePath,
        ModProfile profile,
        IProgress<string> progress,
        CancellationToken cancellationToken = default)
    {
        var backend = ResolveBackend(profile.LaunchBackendKind);
        progress.Report($"Launch backend: {backend.Kind}.");
        var context = CreateBackendContext(gamePath, profile, progress);
        var plan = await backend.PrepareAsync(context, progress, cancellationToken);
        progress.Report($"Starting: {plan.ExecutablePath}");
        try
        {
            var process = _launchPlanExecutor.Start(plan, progress);
            AttachRuntimeLease(process, plan.RuntimeLease);
            return process;
        }
        catch
        {
            if (plan.RuntimeLease is not null)
            {
                await plan.RuntimeLease.DisposeAsync();
            }

            throw;
        }
    }

    private ProfileLaunchBackendContext CreateBackendContext(
        string gamePath,
        ModProfile profile,
        IProgress<string> progress)
    {
        if (profile.IsStandalone || _profileManager is null || string.IsNullOrWhiteSpace(gamePath))
        {
            return new ProfileLaunchBackendContext(gamePath, profile);
        }

        var workspace = _profileManager.EnsureProfileFolderPath(profile, progress);

        var fileLayerPlan = FileLayerPlan.CreateLinkedWorkspace(gamePath, profile, workspace);
        var overlayManifest = _overlayManifestBuilder.BuildLinkedWorkspace(profile, fileLayerPlan, workspace);
        return new ProfileLaunchBackendContext(gamePath, profile, fileLayerPlan, overlayManifest);
    }

    private IProfileLaunchBackend ResolveBackend(LaunchBackendKind kind)
    {
        if (_backends.TryGetValue(kind, out var backend))
        {
            return backend;
        }

        throw new InvalidOperationException(kind == LaunchBackendKind.VirtualFileSystem
            ? "Для профиля выбран USVFS, но его компоненты недоступны. Запустите экспериментальную сборку с файлами usvfs_x64.dll и usvfs_proxy_x64.exe либо выберите Workspace в настройках профиля."
            : $"Система запуска профиля недоступна: {kind}.");
    }

    private static void AttachRuntimeLease(Process process, IAsyncDisposable? runtimeLease)
    {
        if (runtimeLease is null)
        {
            return;
        }

        var disposed = 0;
        async void DisposeRuntime(object? sender, EventArgs args)
        {
            if (Interlocked.Exchange(ref disposed, 1) != 0)
            {
                return;
            }

            try
            {
                await runtimeLease.DisposeAsync();
            }
            catch
            {
                // Runtime cleanup must not crash the launcher after the game exits.
            }
        }

        process.EnableRaisingEvents = true;
        process.Exited += DisposeRuntime;
        if (process.HasExited)
        {
            DisposeRuntime(process, EventArgs.Empty);
        }
    }
}

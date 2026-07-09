using StalkerModLauncher.Models;

namespace StalkerModLauncher.Services;

public sealed class UsvfsLaunchBackend : IProfileLaunchBackend
{
    private readonly IUsvfsRuntime _runtime;
    private readonly string? _runtimeDirectory;
    private readonly UsvfsMappingPlanBuilder _mappingPlanBuilder = new();
    private readonly ProfileLaunchPlanResolver _launchPlanResolver = new();

    public UsvfsLaunchBackend(IUsvfsRuntime runtime, string? runtimeDirectory = null)
    {
        _runtime = runtime;
        _runtimeDirectory = runtimeDirectory;
    }

    public LaunchBackendKind Kind => LaunchBackendKind.VirtualFileSystem;

    public Task<LaunchPlan> PrepareAsync(
        ProfileLaunchBackendContext context,
        IProgress<string> progress,
        CancellationToken cancellationToken = default)
    {
        if (!UsvfsFeatureGate.IsEnabled())
        {
            throw new InvalidOperationException(
                $"Official USVFS backend is disabled. Set {UsvfsFeatureGate.EnableEnvironmentVariable}=1 to enable it for research builds.");
        }

        if (context.Profile.IsStandalone)
        {
            throw new InvalidOperationException("USVFS backend is currently available only for non-standalone layered profiles.");
        }

        var runtimeFiles = UsvfsRuntimeFiles.Check(_runtimeDirectory);
        if (!runtimeFiles.IsReady)
        {
            throw new FileNotFoundException(runtimeFiles.MissingFilesMessage());
        }

        if (context.FileLayerPlan is null || context.OverlayManifest is null)
        {
            throw new InvalidOperationException("USVFS backend requires FileLayerPlan and OverlayManifest.");
        }

        var profile = context.Profile;
        var mappingPlan = _mappingPlanBuilder.Build(context.FileLayerPlan, context.OverlayManifest);
        var launchResolution = _launchPlanResolver.PreviewVirtualFileSystem(profile, context.FileLayerPlan);
        if (!launchResolution.IsReady || launchResolution.Plan is null)
        {
            throw new InvalidOperationException(launchResolution.Error ?? "USVFS launch plan is not ready.");
        }

        progress.Report($"USVFS executable source: {launchResolution.Executable?.SourceName ?? "unknown"}.");
        progress.Report($"USVFS virtual root: {mappingPlan.VirtualRoot}");

        var launchRequest = new UsvfsProcessLaunchRequest(
            launchResolution.Plan.ExecutablePath,
            launchResolution.Plan.Arguments,
            launchResolution.Plan.WorkingDirectory);
        var session = _runtime.CreateSession(
            mappingPlan,
            new UsvfsRuntimeOptions($"stalker_launcher_{profile.Id[..Math.Min(profile.Id.Length, 8)]}"),
            progress);

        var processStarter = new Func<LaunchPlan, IProgress<string>?, System.Diagnostics.Process>(
            (_, startProgress) => session.StartProcess(launchRequest, startProgress, cancellationToken));

        return Task.FromResult(new LaunchPlan(
            LaunchBackendKind.VirtualFileSystem,
            launchRequest.ExecutablePath,
            launchRequest.Arguments,
            launchRequest.WorkingDirectory,
            session,
            processStarter));
    }
}

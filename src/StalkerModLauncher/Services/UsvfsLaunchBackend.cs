using StalkerModLauncher.Models;

namespace StalkerModLauncher.Services;

public sealed class UsvfsLaunchBackend : IProfileLaunchBackend
{
    private readonly IUsvfsRuntime _runtime;
    private readonly IUsvfsRuntime _x86Runtime;
    private readonly string? _runtimeDirectory;
    private readonly UsvfsMappingPlanBuilder _mappingPlanBuilder = new();
    private readonly ProfileLaunchPlanResolver _launchPlanResolver = new();
    private readonly UsvfsProfileDataPreparer _profileDataPreparer = new();
    private readonly AnomalyUsvfsLaunchTargetResolver _anomalyLaunchTargetResolver = new();
    private readonly UsvfsExecutableBootstrapper _executableBootstrapper = new();

    public UsvfsLaunchBackend(
        IUsvfsRuntime runtime,
        string? runtimeDirectory = null,
        IUsvfsRuntime? x86Runtime = null)
    {
        _runtime = runtime;
        _x86Runtime = x86Runtime ?? runtime;
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
                $"Official USVFS backend is unavailable. Put {UsvfsRuntimeFiles.DllFileName} and " +
                $"{UsvfsRuntimeFiles.ProxyFileName} next to the launcher or set " +
                $"{UsvfsFeatureGate.EnableEnvironmentVariable}=1 for a research build.");
        }

        if (context.Profile.IsStandalone)
        {
            throw new InvalidOperationException("USVFS backend is currently available only for non-standalone layered profiles.");
        }

        if (context.FileLayerPlan is null || context.OverlayManifest is null)
        {
            throw new InvalidOperationException("USVFS backend requires FileLayerPlan and OverlayManifest.");
        }

        var profile = context.Profile;
        var profileWorkspace = Path.GetFullPath(Path.Combine(context.OverlayManifest.WriteOverlayRoot, "..", ".."));
        var profileFsgamePath = _profileDataPreparer.Prepare(
            context.FileLayerPlan,
            context.OverlayManifest,
            profileWorkspace,
            progress,
            cancellationToken);
        var launchResolution = _launchPlanResolver.PreviewVirtualFileSystem(profile, context.FileLayerPlan);
        if (!launchResolution.IsReady || launchResolution.Plan is null)
        {
            throw new InvalidOperationException(launchResolution.Error ?? "USVFS launch plan is not ready.");
        }

        var launchTarget = _anomalyLaunchTargetResolver.Resolve(profile, context.FileLayerPlan, launchResolution);
        if (launchTarget.BypassedLauncher)
        {
            progress.Report(
                $"USVFS Anomaly manual engine selection: starting {Path.GetFileName(launchTarget.ExecutablePath)} directly instead of AnomalyLauncher.exe.");
        }

        progress.Report($"USVFS executable source: {launchTarget.SourceName}.");
        var architecture = WindowsExecutableArchitectureDetector.Detect(launchTarget.ExecutablePath);
        var runtimeFiles = UsvfsRuntimeFiles.Check(_runtimeDirectory);
        if (!runtimeFiles.IsReadyFor(architecture))
        {
            throw new FileNotFoundException(runtimeFiles.MissingFilesMessage(architecture));
        }

        progress.Report(architecture == WindowsExecutableArchitecture.X86
            ? "USVFS architecture: x86 target through same-bitness host."
            : "USVFS architecture: x64 target.");

        var usePhysicalAnomalyRoot = IsAnomalyEngine(launchTarget.ExecutableRelativePath);
        var usePhysicalBaseGameRoot = ShouldUsePhysicalBaseGameRoot(context.FileLayerPlan, launchTarget);
        var usePhysicalArchiveRoot = RequiresPhysicalArchiveRoot(context.FileLayerPlan);
        UsvfsBootstrapResult? bootstrap = null;
        if (usePhysicalBaseGameRoot)
        {
            _executableBootstrapper.Clear(profileWorkspace);
        }
        else
        {
            bootstrap = _executableBootstrapper.Prepare(
                context.FileLayerPlan,
                launchTarget,
                profileWorkspace,
                progress,
                cancellationToken);
            MaterializeBootstrapFsgame(profileFsgamePath, bootstrap.RootPath);
        }

        var usePhysicalGameRoot = usePhysicalAnomalyRoot || usePhysicalBaseGameRoot || usePhysicalArchiveRoot;
        var virtualRoot = usePhysicalGameRoot
            ? context.FileLayerPlan.BaseGame.RootPath
            : bootstrap!.RootPath;
        var mappingPlan = _mappingPlanBuilder.Build(
            context.FileLayerPlan,
            context.OverlayManifest,
            virtualRoot);
        var workingDirectory = usePhysicalGameRoot
            ? launchTarget.WorkingDirectory
            : ResolveBootstrapWorkingDirectory(
                context.FileLayerPlan.BaseGame.RootPath,
                launchTarget.WorkingDirectory,
                bootstrap!.RootPath);
        var executablePath = usePhysicalBaseGameRoot
            ? launchTarget.ExecutablePath
            : bootstrap!.ExecutablePath;
        progress.Report(usePhysicalBaseGameRoot
            ? "USVFS root strategy: physical game root for base executable."
            : usePhysicalAnomalyRoot
                ? "USVFS root strategy: physical Anomaly root."
                : usePhysicalArchiveRoot
                    ? "USVFS root strategy: physical game root for X-Ray archive directories."
                : "USVFS root strategy: isolated bootstrap root.");
        progress.Report($"USVFS virtual root: {mappingPlan.VirtualRoot}");

        var launchRequest = new UsvfsProcessLaunchRequest(
            executablePath,
            launchTarget.Arguments,
            workingDirectory);
        var selectedRuntime = architecture == WindowsExecutableArchitecture.X86
            ? _x86Runtime
            : _runtime;
        var session = selectedRuntime.CreateSession(
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

    private static void MaterializeBootstrapFsgame(string? profileFsgamePath, string bootstrapRoot)
    {
        if (string.IsNullOrWhiteSpace(profileFsgamePath) || !File.Exists(profileFsgamePath))
        {
            return;
        }

        File.Copy(profileFsgamePath, Path.Combine(bootstrapRoot, "fsgame.ltx"), overwrite: true);
    }

    private static bool IsAnomalyEngine(string executableRelativePath)
    {
        var fileName = Path.GetFileName(executableRelativePath);
        return fileName.StartsWith("AnomalyDX", StringComparison.OrdinalIgnoreCase) &&
               fileName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase);
    }

    private static bool RequiresPhysicalArchiveRoot(FileLayerPlan layerPlan)
    {
        var fsgame = layerPlan.FindFinalFile("fsgame.ltx");
        if (fsgame is null)
        {
            return false;
        }

        try
        {
            // X-Ray 1.6 discovers patch/resource archives through these root directories.
            // Keeping the real game root preserves that discovery order while USVFS overlays mods on top.
            return File.ReadLines(fsgame.FullPath, XRayTextEncoding.Config)
                .Any(line => line.TrimStart().StartsWith(
                    "$arch_dir_",
                    StringComparison.OrdinalIgnoreCase));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static bool ShouldUsePhysicalBaseGameRoot(
        FileLayerPlan layerPlan,
        UsvfsLaunchTarget launchTarget)
    {
        var executable = layerPlan.FindFinalFile(launchTarget.ExecutableRelativePath);
        if (executable?.Layer.Kind != FileLayerKind.BaseGame)
        {
            return false;
        }

        var executableDirectory = Path.GetDirectoryName(launchTarget.ExecutableRelativePath) ?? string.Empty;
        foreach (var mod in layerPlan.Mods.Where(layer => Directory.Exists(layer.RootPath)))
        {
            var modExecutableDirectory = executableDirectory.Length == 0
                ? mod.RootPath
                : Path.Combine(mod.RootPath, executableDirectory);
            if (!Directory.Exists(modExecutableDirectory))
            {
                continue;
            }

            if (Directory.EnumerateFiles(modExecutableDirectory, "*", SearchOption.TopDirectoryOnly)
                .Any(IsEngineLoaderFile))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsEngineLoaderFile(string path)
    {
        var extension = Path.GetExtension(path);
        return extension.Equals(".exe", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".dll", StringComparison.OrdinalIgnoreCase);
    }

    private static string ResolveBootstrapWorkingDirectory(
        string baseGameRoot,
        string sourceWorkingDirectory,
        string bootstrapRoot)
    {
        var relative = Path.GetRelativePath(
            Path.GetFullPath(baseGameRoot),
            Path.GetFullPath(sourceWorkingDirectory));
        if (relative == ".")
        {
            return bootstrapRoot;
        }

        FileSystemSafety.EnsureRelativePath(relative, "USVFS working directory");
        var workingDirectory = FileSystemSafety.ResolvePathInside(
            bootstrapRoot,
            relative,
            "USVFS working directory");
        Directory.CreateDirectory(workingDirectory);
        return workingDirectory;
    }
}

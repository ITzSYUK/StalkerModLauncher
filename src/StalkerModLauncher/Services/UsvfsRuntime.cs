using StalkerModLauncher.Models;

namespace StalkerModLauncher.Services;

public sealed class UsvfsRuntime(IUsvfsNativeApi nativeApi) : IUsvfsRuntime
{
    public async Task<UsvfsProcessLaunchResult> RunAsync(
        UsvfsMappingPlan mappingPlan,
        UsvfsProcessLaunchRequest launchRequest,
        UsvfsRuntimeOptions options,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(mappingPlan);
        ArgumentNullException.ThrowIfNull(launchRequest);
        ArgumentNullException.ThrowIfNull(options);

        ValidateLaunchRequest(launchRequest);
        var parameters = IntPtr.Zero;
        try
        {
            parameters = nativeApi.CreateParameters();
            nativeApi.SetInstanceName(parameters, options.InstanceName);
            nativeApi.SetDebugMode(parameters, options.DebugMode);
            nativeApi.SetLogLevel(parameters, UsvfsLogLevel.Debug);
            nativeApi.SetCrashDumpType(parameters, UsvfsCrashDumpType.None);
            nativeApi.SetCrashDumpPath(parameters, string.Empty);
            nativeApi.InitLogging(options.EnableLogging);

            if (!nativeApi.CreateVfs(parameters))
            {
                throw new InvalidOperationException("USVFS did not create a virtual file system instance.");
            }

            nativeApi.ClearVirtualMappings();
            ApplyMappings(mappingPlan, progress);

            var commandLine = BuildCommandLine(launchRequest.ExecutablePath, launchRequest.Arguments);
            progress?.Report($"USVFS starting: {launchRequest.ExecutablePath}");
            var process = await nativeApi.CreateProcessHookedAsync(
                launchRequest.ExecutablePath,
                commandLine,
                launchRequest.WorkingDirectory,
                cancellationToken);

            var exitCode = await process.ExitCodeTask;
            return new UsvfsProcessLaunchResult(exitCode, process.ProcessId);
        }
        finally
        {
            nativeApi.DisconnectVfs();
            nativeApi.FreeParameters(parameters);
        }
    }

    private void ApplyMappings(UsvfsMappingPlan mappingPlan, IProgress<string>? progress)
    {
        foreach (var operation in mappingPlan.Operations.OrderBy(operation => operation.Order))
        {
            var flags = BuildFlags(operation);
            var ok = operation.Kind switch
            {
                UsvfsMappingKind.DirectoryStatic => nativeApi.LinkDirectoryStatic(
                    operation.SourcePath,
                    operation.DestinationPath,
                    flags),
                UsvfsMappingKind.File => nativeApi.LinkFile(
                    operation.SourcePath,
                    operation.DestinationPath,
                    flags),
                _ => throw new InvalidOperationException($"Unsupported USVFS mapping kind: {operation.Kind}.")
            };

            if (!ok)
            {
                throw new InvalidOperationException(
                    $"USVFS failed to map '{operation.SourcePath}' to '{operation.DestinationPath}'.");
            }

            progress?.Report($"USVFS mapped: {operation.SourceName}");
        }
    }

    private static UsvfsLinkFlags BuildFlags(UsvfsMappingOperation operation)
    {
        var flags = UsvfsLinkFlags.Recursive;
        if (operation.MonitorChanges)
        {
            flags |= UsvfsLinkFlags.MonitorChanges;
        }

        if (operation.CreateTarget)
        {
            flags |= UsvfsLinkFlags.CreateTarget;
        }

        return flags;
    }

    private static void ValidateLaunchRequest(UsvfsProcessLaunchRequest launchRequest)
    {
        if (string.IsNullOrWhiteSpace(launchRequest.ExecutablePath))
        {
            throw new ArgumentException("USVFS launch executable path cannot be empty.", nameof(launchRequest));
        }

        if (string.IsNullOrWhiteSpace(launchRequest.WorkingDirectory))
        {
            throw new ArgumentException("USVFS launch working directory cannot be empty.", nameof(launchRequest));
        }
    }

    private static string BuildCommandLine(string executablePath, string? arguments)
    {
        var commandLine = Quote(executablePath);
        if (!string.IsNullOrWhiteSpace(arguments))
        {
            commandLine += " " + arguments.Trim();
        }

        return commandLine;
    }

    private static string Quote(string value)
    {
        return "\"" + value.Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";
    }
}

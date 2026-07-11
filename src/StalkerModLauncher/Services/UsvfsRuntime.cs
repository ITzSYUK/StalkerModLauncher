using System.Diagnostics;
using StalkerModLauncher.Models;

namespace StalkerModLauncher.Services;

public sealed class UsvfsRuntime(IUsvfsNativeApi nativeApi) : IUsvfsRuntime
{
    public IUsvfsRuntimeSession CreateSession(
        UsvfsMappingPlan mappingPlan,
        UsvfsRuntimeOptions options,
        IProgress<string>? progress = null)
    {
        ArgumentNullException.ThrowIfNull(mappingPlan);
        ArgumentNullException.ThrowIfNull(options);

        var parameters = IntPtr.Zero;
        var reservation = UsvfsSessionReservation.Acquire();
        try
        {
            parameters = nativeApi.CreateParameters();
            ConfigureParameters(parameters, options);
            nativeApi.InitLogging(options.EnableLogging);

            if (!nativeApi.CreateVfs(parameters))
            {
                throw new InvalidOperationException("USVFS did not create a virtual file system instance.");
            }

            nativeApi.ClearVirtualMappings();
            ApplyMappings(mappingPlan, progress);
            return new UsvfsRuntimeSession(nativeApi, parameters, reservation);
        }
        catch
        {
            try
            {
                nativeApi.DisconnectVfs();
            }
            finally
            {
                nativeApi.FreeParameters(parameters);
                reservation.Dispose();
            }

            throw;
        }
    }

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
        await using var session = CreateSession(mappingPlan, options, progress);
        var startedProcess = session.StartProcess(launchRequest, progress, cancellationToken);
        var exitCode = await session.GetExitCodeAsync(cancellationToken);
        return new UsvfsProcessLaunchResult(exitCode, startedProcess.Id);
    }

    private void ConfigureParameters(IntPtr parameters, UsvfsRuntimeOptions options)
    {
        nativeApi.SetInstanceName(parameters, options.InstanceName);
        nativeApi.SetDebugMode(parameters, options.DebugMode);
        nativeApi.SetLogLevel(parameters, UsvfsLogLevel.Debug);
        nativeApi.SetCrashDumpType(parameters, UsvfsCrashDumpType.None);
        nativeApi.SetCrashDumpPath(parameters, string.Empty);
    }

    internal static string BuildCommandLine(string executablePath, string? arguments)
    {
        var commandLine = Quote(executablePath);
        if (!string.IsNullOrWhiteSpace(arguments))
        {
            commandLine += " " + arguments.Trim();
        }

        return commandLine;
    }

    private void ApplyMappings(UsvfsMappingPlan mappingPlan, IProgress<string>? progress)
    {
        foreach (var operation in mappingPlan.Operations.OrderBy(operation => operation.Order))
        {
            ApplyMapping(operation, progress);
        }
    }

    private void ApplyMapping(UsvfsMappingOperation operation, IProgress<string>? progress)
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

    internal static void ValidateLaunchRequest(UsvfsProcessLaunchRequest launchRequest)
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

    private static string Quote(string value)
    {
        return "\"" + value.Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";
    }
}

public sealed class UsvfsRuntimeSession : IUsvfsRuntimeSession
{
    private readonly IUsvfsNativeApi _nativeApi;
    private readonly IntPtr _parameters;
    private readonly UsvfsSessionReservation _reservation;
    private readonly object _sync = new();
    private Task<int>? _exitCodeTask;
    private bool _disposed;

    internal UsvfsRuntimeSession(
        IUsvfsNativeApi nativeApi,
        IntPtr parameters,
        UsvfsSessionReservation reservation)
    {
        _nativeApi = nativeApi;
        _parameters = parameters;
        _reservation = reservation;
    }

    public Process StartProcess(
        UsvfsProcessLaunchRequest launchRequest,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        UsvfsRuntime.ValidateLaunchRequest(launchRequest);
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_exitCodeTask is not null)
            {
                throw new InvalidOperationException("This USVFS session already started a process.");
            }

            var commandLine = UsvfsRuntime.BuildCommandLine(launchRequest.ExecutablePath, launchRequest.Arguments);
            progress?.Report($"USVFS starting: {launchRequest.ExecutablePath}");
            var handle = _nativeApi.CreateProcessHookedAsync(
                    launchRequest.ExecutablePath,
                    commandLine,
                    launchRequest.WorkingDirectory,
                    cancellationToken)
                .GetAwaiter()
                .GetResult();

            _exitCodeTask = handle.ExitCodeTask;
            return Process.GetProcessById(handle.ProcessId);
        }
    }

    public async Task<int> GetExitCodeAsync(CancellationToken cancellationToken = default)
    {
        Task<int>? task;
        lock (_sync)
        {
            task = _exitCodeTask;
        }

        if (task is null)
        {
            throw new InvalidOperationException("The USVFS session has not started a process.");
        }

        return await task.WaitAsync(cancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        lock (_sync)
        {
            if (_disposed)
            {
                return ValueTask.CompletedTask;
            }

            _disposed = true;
        }

        try
        {
            _nativeApi.DisconnectVfs();
        }
        finally
        {
            try
            {
                _nativeApi.FreeParameters(_parameters);
            }
            finally
            {
                _reservation.Dispose();
            }
        }

        return ValueTask.CompletedTask;
    }
}

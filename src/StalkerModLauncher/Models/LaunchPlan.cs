namespace StalkerModLauncher.Models;

public sealed class LaunchPlan
{
    public LaunchPlan(
        LaunchBackendKind backendKind,
        string executablePath,
        string? arguments,
        string workingDirectory,
        IAsyncDisposable? runtimeLease = null,
        Func<LaunchPlan, IProgress<string>?, System.Diagnostics.Process>? processStarter = null)
    {
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            throw new ArgumentException("Launch executable path cannot be empty.", nameof(executablePath));
        }

        if (string.IsNullOrWhiteSpace(workingDirectory))
        {
            throw new ArgumentException("Launch working directory cannot be empty.", nameof(workingDirectory));
        }

        BackendKind = backendKind;
        ExecutablePath = executablePath;
        Arguments = arguments?.Trim() ?? string.Empty;
        WorkingDirectory = workingDirectory;
        RuntimeLease = runtimeLease;
        ProcessStarter = processStarter;
    }

    public LaunchBackendKind BackendKind { get; }
    public string ExecutablePath { get; }
    public string Arguments { get; }
    public string WorkingDirectory { get; }
    public IAsyncDisposable? RuntimeLease { get; }
    public Func<LaunchPlan, IProgress<string>?, System.Diagnostics.Process>? ProcessStarter { get; }
}

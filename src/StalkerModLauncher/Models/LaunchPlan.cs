namespace StalkerModLauncher.Models;

public sealed class LaunchPlan
{
    public LaunchPlan(
        LaunchBackendKind backendKind,
        string executablePath,
        string? arguments,
        string workingDirectory)
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
    }

    public LaunchBackendKind BackendKind { get; }
    public string ExecutablePath { get; }
    public string Arguments { get; }
    public string WorkingDirectory { get; }
}

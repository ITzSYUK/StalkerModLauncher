namespace StalkerModLauncher.Models;

public sealed record UsvfsProcessLaunchRequest(
    string ExecutablePath,
    string? Arguments,
    string WorkingDirectory);

public sealed record UsvfsProcessLaunchResult(
    int ExitCode,
    int ProcessId);

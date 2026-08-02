namespace StalkerModLauncher.Models;

public sealed record UsvfsRuntimeOptions(
    string InstanceName,
    bool LogToConsole = true,
    bool DebugMode = false,
    string? DiagnosticLogPath = null);

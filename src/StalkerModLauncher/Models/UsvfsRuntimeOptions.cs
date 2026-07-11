namespace StalkerModLauncher.Models;

public sealed record UsvfsRuntimeOptions(
    string InstanceName,
    bool EnableLogging = false,
    bool DebugMode = false);

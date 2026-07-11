namespace StalkerModLauncher.Services;

public static class UsvfsFeatureGate
{
    public const string EnableEnvironmentVariable = "STALKER_MOD_LAUNCHER_ENABLE_OFFICIAL_USVFS";

    public static bool IsEnabled(string? runtimeDirectory = null)
    {
        var value = Environment.GetEnvironmentVariable(EnableEnvironmentVariable);
        var environmentEnabled = string.Equals(value, "1", StringComparison.OrdinalIgnoreCase) ||
                                 string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) ||
                                 string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase);
        return environmentEnabled || UsvfsRuntimeFiles.Check(runtimeDirectory).IsReady;
    }
}

namespace StalkerModLauncher.Services;

public static class UsvfsFeatureGate
{
    public const string EnableEnvironmentVariable = "STALKER_MOD_LAUNCHER_ENABLE_OFFICIAL_USVFS";

    public static bool IsEnabled()
    {
        var value = Environment.GetEnvironmentVariable(EnableEnvironmentVariable);
        return string.Equals(value, "1", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase);
    }
}

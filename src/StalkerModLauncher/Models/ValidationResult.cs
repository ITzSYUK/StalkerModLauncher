namespace StalkerModLauncher.Models;

public sealed class ValidationResult
{
    public bool IsValid { get; init; }
    public string Summary { get; init; } = string.Empty;
    public IReadOnlyList<string> Messages { get; init; } = Array.Empty<string>();
}

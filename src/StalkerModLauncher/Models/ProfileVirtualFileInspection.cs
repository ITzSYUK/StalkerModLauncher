namespace StalkerModLauncher.Models;

public sealed record ProfileVirtualFileInspection(
    string RelativePath,
    bool Exists,
    string ReadSourceDisplay,
    string WriteTargetDisplay,
    string ProvidersDisplay);

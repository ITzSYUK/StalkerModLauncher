namespace StalkerModLauncher.Models;

public enum LaunchBackendKind
{
    LinkedWorkspace = 0,

    // Legacy value kept so old settings can be normalized without breaking startup.
    VirtualFileSystem = 1
}

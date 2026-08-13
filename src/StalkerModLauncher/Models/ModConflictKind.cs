namespace StalkerModLauncher.Models;

public enum ModConflictKind
{
    None,
    Overwrite,
    Overwritten,
    Mixed,
    Redundant,
    Disabled
}

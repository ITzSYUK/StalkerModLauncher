namespace StalkerModLauncher.ViewModels;

public enum ModListFilter
{
    All,
    Conflicts,
    Overwrite,
    Overwritten,
    Mixed,
    Redundant,
    Binaries
}

public sealed record ModListFilterOption(ModListFilter Value, string Name);

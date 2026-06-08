using StalkerModLauncher.Models;

namespace StalkerModLauncher.Services;

public sealed class ModListEditor
{
    public ModEntry Add(ModProfile profile, string sourcePath, string? name = null)
    {
        var mod = new ModEntry
        {
            Name = name ?? Path.GetFileName(sourcePath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)),
            SourcePath = sourcePath,
            IsEnabled = true,
            Order = profile.Mods.Count + 1
        };

        profile.Mods.Add(mod);
        return mod;
    }

    public int Remove(ModProfile profile, IEnumerable<ModEntry> mods)
    {
        var removed = 0;
        foreach (var mod in mods.Distinct().ToList())
        {
            if (profile.Mods.Remove(mod))
            {
                removed++;
            }
        }

        Renumber(profile);
        return removed;
    }

    public bool Move(ModProfile profile, ModEntry source, ModEntry target)
    {
        var oldIndex = profile.Mods.IndexOf(source);
        var newIndex = profile.Mods.IndexOf(target);
        return MoveToIndex(profile, oldIndex, newIndex);
    }

    public bool MoveByOffset(ModProfile profile, ModEntry source, int offset)
    {
        var oldIndex = profile.Mods.IndexOf(source);
        return MoveToIndex(profile, oldIndex, oldIndex + offset);
    }

    public bool MoveToEnd(ModProfile profile, ModEntry source)
    {
        return MoveToIndex(profile, profile.Mods.IndexOf(source), profile.Mods.Count - 1);
    }

    public bool MoveToInsertionIndex(ModProfile profile, ModEntry source, int insertionIndex)
    {
        if (!CollectionReorderer.MoveToInsertionIndex(profile.Mods, source, insertionIndex))
        {
            return false;
        }

        Renumber(profile);
        return true;
    }

    public bool CanMoveByOffset(ModProfile profile, ModEntry source, int offset)
    {
        var oldIndex = profile.Mods.IndexOf(source);
        var newIndex = oldIndex + offset;
        return oldIndex >= 0 && newIndex >= 0 && newIndex < profile.Mods.Count;
    }

    public void Renumber(ModProfile profile)
    {
        for (var index = 0; index < profile.Mods.Count; index++)
        {
            profile.Mods[index].Order = index + 1;
        }
    }

    private bool MoveToIndex(ModProfile profile, int oldIndex, int newIndex)
    {
        if (oldIndex < 0 || newIndex < 0 || newIndex >= profile.Mods.Count || oldIndex == newIndex)
        {
            return false;
        }

        profile.Mods.Move(oldIndex, newIndex);
        Renumber(profile);
        return true;
    }
}

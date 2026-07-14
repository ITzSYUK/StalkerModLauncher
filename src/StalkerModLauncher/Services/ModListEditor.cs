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
        return MoveManyToInsertionIndex(profile, [source], insertionIndex);
    }

    public bool MoveManyToInsertionIndex(
        ModProfile profile,
        IEnumerable<ModEntry> sources,
        int insertionIndex)
    {
        var selected = sources
            .Where(profile.Mods.Contains)
            .Distinct()
            .ToHashSet();
        if (selected.Count == 0)
        {
            return false;
        }

        var original = profile.Mods.ToList();
        var orderedSelection = original.Where(selected.Contains).ToList();
        insertionIndex = Math.Clamp(insertionIndex, 0, original.Count);

        // The insertion index is expressed against the original list. Account for
        // selected rows preceding it before inserting the block into the remainder.
        var removedBeforeInsertion = original
            .Take(insertionIndex)
            .Count(selected.Contains);
        var remainder = original.Where(mod => !selected.Contains(mod)).ToList();
        var adjustedIndex = Math.Clamp(insertionIndex - removedBeforeInsertion, 0, remainder.Count);
        remainder.InsertRange(adjustedIndex, orderedSelection);

        if (original.SequenceEqual(remainder))
        {
            return false;
        }

        ApplyOrder(profile, remainder);
        Renumber(profile);
        return true;
    }

    public bool MoveManyToStart(ModProfile profile, IEnumerable<ModEntry> sources)
    {
        return MoveManyToInsertionIndex(profile, sources, 0);
    }

    public bool MoveManyToEnd(ModProfile profile, IEnumerable<ModEntry> sources)
    {
        return MoveManyToInsertionIndex(profile, sources, profile.Mods.Count);
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

    private static void ApplyOrder(ModProfile profile, IReadOnlyList<ModEntry> desiredOrder)
    {
        for (var targetIndex = 0; targetIndex < desiredOrder.Count; targetIndex++)
        {
            var currentIndex = profile.Mods.IndexOf(desiredOrder[targetIndex]);
            if (currentIndex != targetIndex)
            {
                profile.Mods.Move(currentIndex, targetIndex);
            }
        }
    }
}

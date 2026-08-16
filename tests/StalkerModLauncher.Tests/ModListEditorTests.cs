using StalkerModLauncher.Models;
using StalkerModLauncher.Services;
using Xunit;

namespace StalkerModLauncher.Tests;

public sealed class ModListEditorTests
{
    [Fact]
    public void AddCreatesEnabledModAtEnd()
    {
        var profile = CreateProfile("First");

        var added = ModListEditor.Add(profile, @"D:\Mods\Second");

        Assert.Equal("Second", added.Name);
        Assert.Equal(@"D:\Mods\Second", added.SourcePath);
        Assert.True(added.IsEnabled);
        Assert.Equal(2, added.Order);
        Assert.Same(added, profile.Mods[1]);
    }

    [Fact]
    public void MoveReordersAndRenumbersMods()
    {
        var profile = CreateProfile("First", "Second", "Third");

        var moved = ModListEditor.Move(profile, profile.Mods[0], profile.Mods[2]);

        Assert.True(moved);
        Assert.Equal(["Second", "Third", "First"], profile.Mods.Select(mod => mod.Name));
        Assert.Equal([1, 2, 3], profile.Mods.Select(mod => mod.Order));
    }

    [Fact]
    public void RemoveRemovesExistingModsAndRenumbersRemainder()
    {
        var profile = CreateProfile("First", "Second", "Third", "Fourth");

        var removed = ModListEditor.Remove(profile, [profile.Mods[1], profile.Mods[3]]);

        Assert.Equal(2, removed);
        Assert.Equal(["First", "Third"], profile.Mods.Select(mod => mod.Name));
        Assert.Equal([1, 2], profile.Mods.Select(mod => mod.Order));
    }

    [Fact]
    public void MoveToEndMovesModAndRenumbers()
    {
        var profile = CreateProfile("First", "Second", "Third");

        var moved = ModListEditor.MoveToEnd(profile, profile.Mods[0]);

        Assert.True(moved);
        Assert.Equal(["Second", "Third", "First"], profile.Mods.Select(mod => mod.Name));
        Assert.Equal([1, 2, 3], profile.Mods.Select(mod => mod.Order));
    }

    [Fact]
    public void MoveByOffsetDoesNotMoveOutsideCollection()
    {
        var profile = CreateProfile("First", "Second");

        Assert.False(ModListEditor.CanMoveByOffset(profile, profile.Mods[0], -1));
        Assert.False(ModListEditor.MoveByOffset(profile, profile.Mods[0], -1));
        Assert.Equal(["First", "Second"], profile.Mods.Select(mod => mod.Name));
    }

    [Fact]
    public void MoveToInsertionIndexReordersAndRenumbersMods()
    {
        var profile = CreateProfile("First", "Second", "Third");

        var moved = ModListEditor.MoveToInsertionIndex(profile, profile.Mods[0], 3);

        Assert.True(moved);
        Assert.Equal(["Second", "Third", "First"], profile.Mods.Select(mod => mod.Name));
        Assert.Equal([1, 2, 3], profile.Mods.Select(mod => mod.Order));
    }

    [Fact]
    public void MoveManyToInsertionIndexMovesSelectionAsOrderedBlock()
    {
        var profile = CreateProfile("First", "Second", "Third", "Fourth", "Fifth");
        var selected = new[] { profile.Mods[1], profile.Mods[3] };

        var moved = ModListEditor.MoveManyToInsertionIndex(profile, selected, 5);

        Assert.True(moved);
        Assert.Equal(["First", "Third", "Fifth", "Second", "Fourth"], profile.Mods.Select(mod => mod.Name));
        Assert.Equal([1, 2, 3, 4, 5], profile.Mods.Select(mod => mod.Order));
    }

    [Fact]
    public void MoveManyToStartAndEndPreserveRelativeOrder()
    {
        var profile = CreateProfile("First", "Second", "Third", "Fourth", "Fifth");
        var selected = new[] { profile.Mods[1], profile.Mods[3] };

        Assert.True(ModListEditor.MoveManyToStart(profile, selected));
        Assert.Equal(["Second", "Fourth", "First", "Third", "Fifth"], profile.Mods.Select(mod => mod.Name));

        Assert.True(ModListEditor.MoveManyToEnd(profile, selected));
        Assert.Equal(["First", "Third", "Fifth", "Second", "Fourth"], profile.Mods.Select(mod => mod.Name));
        Assert.Equal([1, 2, 3, 4, 5], profile.Mods.Select(mod => mod.Order));
    }

    private static ModProfile CreateProfile(params string[] names)
    {
        var profile = new ModProfile();
        foreach (var name in names)
        {
            profile.Mods.Add(new ModEntry { Name = name, Order = 99 });
        }

        return profile;
    }
}

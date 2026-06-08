using StalkerModLauncher.Models;
using StalkerModLauncher.Services;
using Xunit;

namespace StalkerModLauncher.Tests;

public sealed class ModListEditorTests
{
    private readonly ModListEditor _editor = new();

    [Fact]
    public void Add_CreatesEnabledModAtEnd()
    {
        var profile = CreateProfile("First");

        var added = _editor.Add(profile, @"D:\Mods\Second");

        Assert.Equal("Second", added.Name);
        Assert.Equal(@"D:\Mods\Second", added.SourcePath);
        Assert.True(added.IsEnabled);
        Assert.Equal(2, added.Order);
        Assert.Same(added, profile.Mods[1]);
    }

    [Fact]
    public void Move_ReordersAndRenumbersMods()
    {
        var profile = CreateProfile("First", "Second", "Third");

        var moved = _editor.Move(profile, profile.Mods[0], profile.Mods[2]);

        Assert.True(moved);
        Assert.Equal(["Second", "Third", "First"], profile.Mods.Select(mod => mod.Name));
        Assert.Equal([1, 2, 3], profile.Mods.Select(mod => mod.Order));
    }

    [Fact]
    public void Remove_RemovesExistingModsAndRenumbersRemainder()
    {
        var profile = CreateProfile("First", "Second", "Third", "Fourth");

        var removed = _editor.Remove(profile, [profile.Mods[1], profile.Mods[3]]);

        Assert.Equal(2, removed);
        Assert.Equal(["First", "Third"], profile.Mods.Select(mod => mod.Name));
        Assert.Equal([1, 2], profile.Mods.Select(mod => mod.Order));
    }

    [Fact]
    public void MoveToEnd_MovesModAndRenumbers()
    {
        var profile = CreateProfile("First", "Second", "Third");

        var moved = _editor.MoveToEnd(profile, profile.Mods[0]);

        Assert.True(moved);
        Assert.Equal(["Second", "Third", "First"], profile.Mods.Select(mod => mod.Name));
        Assert.Equal([1, 2, 3], profile.Mods.Select(mod => mod.Order));
    }

    [Fact]
    public void MoveByOffset_DoesNotMoveOutsideCollection()
    {
        var profile = CreateProfile("First", "Second");

        Assert.False(_editor.CanMoveByOffset(profile, profile.Mods[0], -1));
        Assert.False(_editor.MoveByOffset(profile, profile.Mods[0], -1));
        Assert.Equal(["First", "Second"], profile.Mods.Select(mod => mod.Name));
    }

    [Fact]
    public void MoveToInsertionIndex_ReordersAndRenumbersMods()
    {
        var profile = CreateProfile("First", "Second", "Third");

        var moved = _editor.MoveToInsertionIndex(profile, profile.Mods[0], 3);

        Assert.True(moved);
        Assert.Equal(["Second", "Third", "First"], profile.Mods.Select(mod => mod.Name));
        Assert.Equal([1, 2, 3], profile.Mods.Select(mod => mod.Order));
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

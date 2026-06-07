using StalkerModLauncher.Models;
using StalkerModLauncher.Services;
using Xunit;

namespace StalkerModLauncher.Tests;

public sealed class ProfileManagerTests
{
    private readonly FakeWorkspaceManager _workspaceManager = new();
    private readonly ProfileManager _manager;

    public ProfileManagerTests()
    {
        var root = Path.Combine(Path.GetTempPath(), "StalkerModLauncherTests", Guid.NewGuid().ToString("N"));
        _manager = new ProfileManager(new AppPaths(root, Path.Combine(root, "workspaces"), false), _workspaceManager);
    }

    [Fact]
    public void Create_UsesUniqueNameAndSeparateWorkspace()
    {
        var existing = new List<ModProfile> { new() { Name = "Profile 2" } };

        var created = _manager.Create(existing, @"D:\Games\STALKER");

        Assert.Equal("Profile 2 (2)", created.Name);
        Assert.Equal(@"D:\Games\STALKER", created.GameInstallPath);
        Assert.EndsWith($"Profile 2 (2)-{created.Id[..8]}", created.WorkspacePath);
    }

    [Fact]
    public void Duplicate_CopiesConfigurationButUsesNewIdsAndWorkspace()
    {
        var source = new ModProfile
        {
            Name = "Zona",
            Description = "Description",
            GameInstallPath = @"D:\Games\STALKER",
            WorkspacePath = @"D:\OldWorkspace",
            TotalPlaytimeSeconds = 500,
            LastPlayedAt = DateTime.Now
        };
        source.Mods.Add(new ModEntry { Name = "Patch", SourcePath = @"D:\Mods\Patch", Order = 1, Notes = "note" });

        var duplicate = _manager.Duplicate([source], source);

        Assert.Equal("Zona — копия", duplicate.Name);
        Assert.NotEqual(source.Id, duplicate.Id);
        Assert.NotEqual(source.WorkspacePath, duplicate.WorkspacePath);
        Assert.EndsWith($"Zona — копия-{duplicate.Id[..8]}", duplicate.WorkspacePath);
        Assert.Equal(0, duplicate.TotalPlaytimeSeconds);
        Assert.Null(duplicate.LastPlayedAt);
        Assert.Single(duplicate.Mods);
        Assert.NotSame(source.Mods[0], duplicate.Mods[0]);
        Assert.NotEqual(source.Mods[0].Id, duplicate.Mods[0].Id);
        Assert.Equal(source.Mods[0].SourcePath, duplicate.Mods[0].SourcePath);
    }

    [Fact]
    public void Duplicate_SanitizesAndLimitsReadableWorkspaceName()
    {
        var source = new ModProfile { Name = new string('A', 100) + ": invalid." };

        var duplicate = _manager.Duplicate([source], source);
        var directoryName = Path.GetFileName(duplicate.WorkspacePath);

        Assert.DoesNotContain(':', directoryName);
        Assert.True(directoryName.Length <= 89);
        Assert.EndsWith(duplicate.Id[..8], directoryName);
    }

    [Fact]
    public void Delete_StandaloneProfileNeverDeletesModOrWorkspace()
    {
        var profile = new ModProfile { Name = "Standalone", IsStandalone = true, WorkspacePath = @"D:\Mods\Standalone" };
        var profiles = new List<ModProfile> { profile };

        var selected = _manager.Delete(profiles, profile);

        Assert.Null(selected);
        Assert.Empty(profiles);
        Assert.Empty(_workspaceManager.DeletedProfiles);
    }

    [Fact]
    public void Delete_OverlayProfileDeletesOnlyItsWorkspaceThenSelectsRemainder()
    {
        var removed = new ModProfile { Name = "Removed", GameInstallPath = @"D:\Game" };
        var remaining = new ModProfile { Name = "Remaining" };
        var profiles = new List<ModProfile> { removed, remaining };

        var selected = _manager.Delete(profiles, removed);

        Assert.Same(remaining, selected);
        Assert.Single(_workspaceManager.DeletedProfiles);
        Assert.Same(removed, _workspaceManager.DeletedProfiles[0]);
    }

    [Fact]
    public void Delete_KeepsProfileWhenWorkspaceDeletionFails()
    {
        var profile = new ModProfile { Name = "Keep me" };
        var profiles = new List<ModProfile> { profile };
        _workspaceManager.DeleteException = new IOException("Workspace is busy");

        Assert.Throws<IOException>(() => _manager.Delete(profiles, profile));

        Assert.Single(profiles);
        Assert.Same(profile, profiles[0]);
    }

    [Fact]
    public void PrepareImported_AssignsDefaultsAndUniqueName()
    {
        var existing = new List<ModProfile> { new() { Name = "Imported" } };
        var imported = new ModProfile { Id = string.Empty, Name = "Imported", ExecutableRelativePath = string.Empty, IsRunning = true };

        _manager.PrepareImported(existing, imported);

        Assert.Equal("Imported (2)", imported.Name);
        Assert.False(string.IsNullOrWhiteSpace(imported.Id));
        Assert.Equal(@"bin\xr_3da.exe", imported.ExecutableRelativePath);
        Assert.False(imported.IsRunning);
    }

    private sealed class FakeWorkspaceManager : IProfileWorkspaceManager
    {
        public List<ModProfile> DeletedProfiles { get; } = [];
        public Exception? DeleteException { get; set; }

        public void DeleteProfileWorkspace(ModProfile profile, string gamePath)
        {
            if (DeleteException is not null)
            {
                throw DeleteException;
            }

            DeletedProfiles.Add(profile);
        }
    }
}

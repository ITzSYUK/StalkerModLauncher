using StalkerModLauncher.Models;
using StalkerModLauncher.Services;
using Xunit;

namespace StalkerModLauncher.Tests;

public sealed class FileLayerPlanTests
{
    [Fact]
    public void CreateLinkedWorkspace_OrdersBaseGameEnabledModsAndUserData()
    {
        var gamePath = Path.Combine(Path.GetTempPath(), "game");
        var workspacePath = Path.Combine(Path.GetTempPath(), "workspace");
        var profile = new ModProfile { Name = "Layered profile" };
        var lowerPriorityMod = new ModEntry
        {
            Id = "lower",
            Name = "Main mod",
            SourcePath = Path.Combine(Path.GetTempPath(), "mods", "main"),
            IsEnabled = true,
            Order = 1
        };
        var higherPriorityMod = new ModEntry
        {
            Id = "higher",
            Name = "Patch",
            SourcePath = Path.Combine(Path.GetTempPath(), "mods", "patch"),
            IsEnabled = true,
            Order = 2
        };
        var disabledMod = new ModEntry
        {
            Id = "disabled",
            Name = "Disabled",
            SourcePath = Path.Combine(Path.GetTempPath(), "mods", "disabled"),
            IsEnabled = false,
            Order = 3
        };
        profile.Mods.Add(higherPriorityMod);
        profile.Mods.Add(disabledMod);
        profile.Mods.Add(lowerPriorityMod);

        var plan = FileLayerPlan.CreateLinkedWorkspace(gamePath, profile, workspacePath);

        Assert.Equal(FileLayerKind.BaseGame, plan.Layers[0].Kind);
        Assert.Equal(Path.GetFullPath(gamePath), plan.BaseGame.RootPath);
        Assert.Equal(["lower", "higher"], plan.Mods.Select(layer => layer.Id));
        Assert.Equal([lowerPriorityMod, higherPriorityMod], plan.Mods.Select(layer => layer.Mod));
        Assert.DoesNotContain(plan.Layers, layer => layer.Id == "disabled");
        Assert.Equal(FileLayerKind.UserData, plan.Layers[^1].Kind);
        Assert.Equal(Path.Combine(Path.GetFullPath(workspacePath), "userdata"), plan.UserData.RootPath);
    }

    [Fact]
    public void CreateLinkedWorkspace_RejectsStandaloneProfiles()
    {
        var profile = new ModProfile { IsStandalone = true };

        Assert.Throws<InvalidOperationException>(
            () => FileLayerPlan.CreateLinkedWorkspace("game", profile, "workspace"));
    }
}

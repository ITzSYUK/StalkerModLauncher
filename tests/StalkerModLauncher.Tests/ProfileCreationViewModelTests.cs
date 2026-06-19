using StalkerModLauncher.Models;
using StalkerModLauncher.Services;
using StalkerModLauncher.ViewModels;
using Xunit;

namespace StalkerModLauncher.Tests;

public sealed class ProfileCreationViewModelTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "StalkerModLauncherTests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public void NextCommand_AutoDetectsExecutableFromHighestPriorityMod()
    {
        var game = CreateDirectory("game");
        CreateFile(game, "bin/xr_3da.exe");
        var mainMod = CreateDirectory("main-mod");
        CreateFile(mainMod, "bin_x64/xrEngine.exe");
        var patch = CreateDirectory("patch");
        CreateFile(patch, "bin_x64/xrEngine.exe");
        var viewModel = new ProfileCreationViewModel(new DialogService())
        {
            Name = "Ликвидация",
            GamePath = game
        };
        viewModel.Mods.Add(new ModEntry { Name = "main", SourcePath = mainMod, Order = 1 });
        viewModel.Mods.Add(new ModEntry { Name = "patch", SourcePath = patch, Order = 2 });

        viewModel.NextCommand.Execute(null);
        viewModel.NextCommand.Execute(null);

        Assert.True(viewModel.IsStepThree);
        Assert.Equal(@"bin_x64\xrEngine.exe", viewModel.ExecutableRelativePath);
        Assert.Contains("мод: patch", viewModel.ExecutableDetectionMessage);
    }

    private string CreateDirectory(string relativePath)
    {
        var path = Path.Combine(_root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void CreateFile(string root, string relativePath)
    {
        var path = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "test");
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}

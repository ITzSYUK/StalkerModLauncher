using System.Diagnostics;
using StalkerModLauncher.Models;
using StalkerModLauncher.Services;
using StalkerModLauncher.ViewModels;
using Xunit;

namespace StalkerModLauncher.Tests;

public sealed class MainViewModelTests
{
    [Fact]
    public async Task NewProfileCommand_RaisesProfileCreationRequest()
    {
        await RunWithViewModelAsync((viewModel, _) =>
        {
            var raised = false;
            viewModel.ProfileCreationRequested += (_, _) => raised = true;

            viewModel.NewProfileCommand.Execute(null);

            Assert.True(raised);
            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task AddCreatedProfile_SelectsProfileAndDoesNotInheritPreviousGamePath()
    {
        await RunWithViewModelAsync((viewModel, root) =>
        {
            var gameRoot = Path.Combine(root, "game");
            var existing = new ModProfile { Name = "Profile", GameInstallPath = gameRoot };
            viewModel.AddCreatedProfile(existing);

            var created = new ModProfile { Name = "Profile" };
            viewModel.AddCreatedProfile(created);

            Assert.Equal("Profile (2)", created.Name);
            Assert.Same(created, viewModel.SelectedProfile);
            Assert.Empty(created.GameInstallPath);
            Assert.Empty(viewModel.GameInstallPath);
            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task DuplicateProfileCommand_CreatesIndependentCopyAndSelectsIt()
    {
        await RunWithViewModelAsync((viewModel, root) =>
        {
            var source = new ModProfile
            {
                Name = "Anomaly",
                Description = "Test profile",
                GameInstallPath = Path.Combine(root, "game"),
                LaunchArguments = "-nointro -test",
                TotalPlaytimeSeconds = 42,
                LastPlayedAt = DateTime.Now
            };
            source.Mods.Add(new ModEntry { Name = "Patch", SourcePath = Path.Combine(root, "mods", "patch"), Order = 1 });
            viewModel.AddCreatedProfile(source);

            Assert.True(viewModel.DuplicateProfileCommand.CanExecute(null));
            viewModel.DuplicateProfileCommand.Execute(null);

            var duplicate = Assert.Single(viewModel.Profiles, profile => profile != source);
            Assert.Same(duplicate, viewModel.SelectedProfile);
            Assert.Equal("Anomaly — копия", duplicate.Name);
            Assert.NotEqual(source.Id, duplicate.Id);
            Assert.Equal(source.GameInstallPath, duplicate.GameInstallPath);
            Assert.NotEqual(source.WorkspacePath, duplicate.WorkspacePath);
            Assert.EndsWith($"Anomaly — копия-{duplicate.Id[..8]}", duplicate.WorkspacePath);
            Assert.Equal(0, duplicate.TotalPlaytimeSeconds);
            Assert.Null(duplicate.LastPlayedAt);
            Assert.Single(duplicate.Mods);
            Assert.NotSame(source.Mods[0], duplicate.Mods[0]);
            Assert.NotEqual(source.Mods[0].Id, duplicate.Mods[0].Id);
            Assert.Equal(source.Mods[0].SourcePath, duplicate.Mods[0].SourcePath);
            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task MoveModToInsertionIndex_ReordersSelectedProfileAndRenumbersMods()
    {
        await RunWithViewModelAsync((viewModel, root) =>
        {
            var profile = new ModProfile { Name = "Overlay", GameInstallPath = Path.Combine(root, "game") };
            profile.Mods.Add(new ModEntry { Name = "Base mod", SourcePath = Path.Combine(root, "mods", "base"), Order = 1 });
            profile.Mods.Add(new ModEntry { Name = "Patch", SourcePath = Path.Combine(root, "mods", "patch"), Order = 2 });
            profile.Mods.Add(new ModEntry { Name = "Fix", SourcePath = Path.Combine(root, "mods", "fix"), Order = 3 });
            viewModel.AddCreatedProfile(profile);

            var moved = profile.Mods[0];
            viewModel.MoveModToInsertionIndex(moved, 3);

            Assert.Equal(["Patch", "Fix", "Base mod"], profile.Mods.Select(mod => mod.Name));
            Assert.Equal([1, 2, 3], profile.Mods.Select(mod => mod.Order));
            Assert.Same(moved, viewModel.SelectedMod);
            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task AddDroppedMods_IgnoresDuplicatesAndStandaloneProfileLimit()
    {
        await RunWithViewModelAsync((viewModel, root) =>
        {
            var firstMod = Directory.CreateDirectory(Path.Combine(root, "mods", "first")).FullName;
            var secondMod = Directory.CreateDirectory(Path.Combine(root, "mods", "second")).FullName;
            var profile = new ModProfile { Name = "Standalone", IsStandalone = true };
            viewModel.AddCreatedProfile(profile);

            viewModel.AddDroppedMods([firstMod, firstMod, secondMod]);

            Assert.Single(profile.Mods);
            Assert.Equal(firstMod, profile.Mods[0].SourcePath);
            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task RunningProfile_BlocksModCommandsAndDirectModMutations()
    {
        await RunWithViewModelAsync((viewModel, root) =>
        {
            var gameRoot = CreateValidGameRoot(root);
            var firstMod = Directory.CreateDirectory(Path.Combine(root, "mods", "first")).FullName;
            var secondMod = Directory.CreateDirectory(Path.Combine(root, "mods", "second")).FullName;
            var extraMod = Directory.CreateDirectory(Path.Combine(root, "mods", "extra")).FullName;
            var profile = new ModProfile
            {
                Name = "Running",
                GameInstallPath = gameRoot,
                ExecutableRelativePath = @"bin\xr_3da.exe"
            };
            profile.Mods.Add(new ModEntry { Name = "First", SourcePath = firstMod, Order = 1 });
            profile.Mods.Add(new ModEntry { Name = "Second", SourcePath = secondMod, Order = 2 });
            viewModel.AddCreatedProfile(profile);
            viewModel.SelectedMod = profile.Mods[0];

            Assert.True(viewModel.IsGameValid);

            profile.IsRunning = true;

            Assert.False(viewModel.CanEditSelectedProfile);
            Assert.False(viewModel.AddModCommand.CanExecute(null));
            Assert.False(viewModel.RemoveModCommand.CanExecute(null));
            Assert.False(viewModel.MoveModDownCommand.CanExecute(null));
            Assert.False(viewModel.InlineRemoveModCommand.CanExecute(profile.Mods[0]));
            Assert.False(viewModel.InlineMoveModDownCommand.CanExecute(profile.Mods[0]));
            Assert.False(viewModel.ScanForModsCommand.CanExecute(null));
            Assert.False(viewModel.LaunchCommand.CanExecute(null));

            viewModel.AddDroppedMods([extraMod]);
            viewModel.MoveModToInsertionIndex(profile.Mods[0], 2);
            viewModel.RemoveMods([profile.Mods[0]]);

            Assert.Equal(["First", "Second"], profile.Mods.Select(mod => mod.Name));
            return Task.CompletedTask;
        });
    }

    private static async Task RunWithViewModelAsync(Func<MainViewModel, string, Task> test)
    {
        var root = Path.Combine(Path.GetTempPath(), "StalkerModLauncherTests", Guid.NewGuid().ToString("N"));
        MainViewModel? viewModel = null;
        try
        {
            viewModel = CreateViewModel(root);
            await WaitForSettingsLoadedAsync(viewModel);
            await test(viewModel, root);
        }
        finally
        {
            if (viewModel is not null)
            {
                await viewModel.CleanupAsync();
            }

            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private static MainViewModel CreateViewModel(string root)
    {
        var paths = new AppPaths(
            Path.Combine(root, "config"),
            Path.Combine(root, "workspaces"),
            preferGameDriveWorkspace: false);
        var settingsStore = new SettingsStore(paths);
        var workspaceBuilder = new WorkspaceBuilder(paths);
        var profileManager = new ProfileManager(paths, new FakeProfileWorkspaceManager());
        var gameValidator = new GameInstallationValidator();

        return new MainViewModel(
            paths,
            settingsStore,
            new LaunchCoordinator(new ThrowingProfileLauncher(), new FakeGameSessionTracker()),
            new DialogService(),
            new ModConflictAnalyzer(),
            new ProfileTransferService(),
            new ModScannerService(),
            new ModListEditor(),
            profileManager,
            new GameExitDiagnosticsService(new ProfileDataPathResolver()),
            new ProfileReadinessService(gameValidator),
            new LaunchPreflightService(gameValidator, profileManager),
            new ApplicationLogService(paths));
    }

    private static async Task WaitForSettingsLoadedAsync(MainViewModel viewModel)
    {
        for (var attempt = 0; attempt < 100; attempt++)
        {
            if (viewModel.ActivityLog.Entries.Any(entry => entry.Contains("Settings loaded.", StringComparison.Ordinal)))
            {
                return;
            }

            await Task.Delay(20);
        }

        throw new TimeoutException("MainViewModel did not finish loading test settings.");
    }

    private static string CreateValidGameRoot(string root)
    {
        var gameRoot = Directory.CreateDirectory(Path.Combine(root, "game")).FullName;
        Directory.CreateDirectory(Path.Combine(gameRoot, "bin"));
        File.WriteAllText(Path.Combine(gameRoot, "bin", "xr_3da.exe"), string.Empty);
        File.WriteAllText(Path.Combine(gameRoot, "fsgame.ltx"), string.Empty);
        return gameRoot;
    }

    private sealed class FakeProfileWorkspaceManager : IProfileWorkspaceManager
    {
        public void DeleteProfileWorkspace(ModProfile profile, string gamePath)
        {
        }

        public void ClearProfileWorkspaceCache(ModProfile profile, string gamePath)
        {
        }
    }

    private sealed class ThrowingProfileLauncher : IProfileLauncher
    {
        public Task<Process> LaunchAsync(
            string gamePath,
            ModProfile profile,
            IProgress<string> progress,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("MainViewModel tests must not launch game processes.");
        }
    }

    private sealed class FakeGameSessionTracker : IGameSessionTracker
    {
        public void ConfigureDiscord(string clientId, Action<string>? diagnostic = null)
        {
        }

        public Task<GameSessionResult> TrackAsync(Process process, string profileName, bool publishDiscordStatus)
        {
            throw new NotSupportedException("MainViewModel tests must not track game processes.");
        }

        public void Dispose()
        {
        }
    }
}

using System.Windows;
using StalkerModLauncher.Models;
using StalkerModLauncher.Services;
using StalkerModLauncher.ViewModels;

namespace StalkerModLauncher.Views;

public partial class ProfileCreationWindow : Window
{
    public ProfileCreationWindow(ProfileCreationViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.Completed += OnCompleted;
    }

    public ModProfile? CreatedProfile { get; private set; }

    private void OnCompleted(object? sender, ModProfile profile)
    {
        CreatedProfile = profile;
        DialogResult = true;
    }

    private ProfileCreationViewModel? ViewModel => DataContext as ProfileCreationViewModel;

    private void GamePath_OnPreviewDragOver(object sender, DragEventArgs e) =>
        SetDirectoryDropEffect(e, acceptMultiple: false);

    private void GamePath_OnPreviewDrop(object sender, DragEventArgs e)
    {
        var directories = GetDroppedDirectories(e);
        if (directories.Length == 1)
        {
            ViewModel?.SetDroppedGamePath(directories[0]);
        }
    }

    private void ModsList_OnPreviewDragOver(object sender, DragEventArgs e) =>
        SetDirectoryDropEffect(e, acceptMultiple: true);

    private void ModsList_OnPreviewDrop(object sender, DragEventArgs e)
    {
        ViewModel?.AddDroppedMods(GetDroppedDirectories(e));
    }

    private void StandalonePath_OnPreviewDragOver(object sender, DragEventArgs e) =>
        SetDirectoryDropEffect(e, acceptMultiple: false);

    private void StandalonePath_OnPreviewDrop(object sender, DragEventArgs e)
    {
        var directories = GetDroppedDirectories(e);
        if (directories.Length == 1)
        {
            ViewModel?.SetDroppedStandalonePath(directories[0]);
        }
    }

    private static void SetDirectoryDropEffect(DragEventArgs e, bool acceptMultiple)
    {
        var directories = GetDroppedDirectories(e);
        e.Effects = directories.Length > 0 && (acceptMultiple || directories.Length == 1)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    private static string[] GetDroppedDirectories(DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop) ||
            e.Data.GetData(DataFormats.FileDrop) is not string[] paths)
        {
            return Array.Empty<string>();
        }

        return paths.Where(Directory.Exists).ToArray();
    }

    private void Window_OnSourceInitialized(object? sender, EventArgs e)
    {
        WindowSystemIntegrationService.Initialize(this);
    }
}

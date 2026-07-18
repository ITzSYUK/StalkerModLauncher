using System.Windows;
using System.Windows.Controls;
using StalkerModLauncher.ViewModels;

namespace StalkerModLauncher.Views.Controls;

public partial class PdaProfileCreationView : UserControl
{
    public PdaProfileCreationView()
    {
        InitializeComponent();
    }

    public event EventHandler? Cancelled;

    private ProfileCreationViewModel? ViewModel => DataContext as ProfileCreationViewModel;

    private void CancelButton_OnClick(object sender, RoutedEventArgs e) =>
        Cancelled?.Invoke(this, EventArgs.Empty);

    private void GamePath_OnPreviewDragOver(object sender, DragEventArgs e) =>
        SetDirectoryDropEffect(e, acceptMultiple: false);

    private void GamePath_OnPreviewDrop(object sender, DragEventArgs e)
    {
        var directories = GetDroppedDirectories(e);
        if (directories.Count == 1)
        {
            ViewModel?.SetDroppedGamePath(directories[0]);
        }
    }

    private void ModsList_OnPreviewDragOver(object sender, DragEventArgs e) =>
        SetDirectoryDropEffect(e, acceptMultiple: true);

    private void ModsList_OnPreviewDrop(object sender, DragEventArgs e) =>
        ViewModel?.AddDroppedMods(GetDroppedDirectories(e));

    private void StandalonePath_OnPreviewDragOver(object sender, DragEventArgs e) =>
        SetDirectoryDropEffect(e, acceptMultiple: false);

    private void StandalonePath_OnPreviewDrop(object sender, DragEventArgs e)
    {
        var directories = GetDroppedDirectories(e);
        if (directories.Count == 1)
        {
            ViewModel?.SetDroppedStandalonePath(directories[0]);
        }
    }

    private static void SetDirectoryDropEffect(DragEventArgs e, bool acceptMultiple)
    {
        var directories = GetDroppedDirectories(e);
        e.Effects = directories.Count > 0 && (acceptMultiple || directories.Count == 1)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    private static IReadOnlyList<string> GetDroppedDirectories(DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop) ||
            e.Data.GetData(DataFormats.FileDrop) is not string[] paths)
        {
            return Array.Empty<string>();
        }

        return paths.Where(Directory.Exists).ToArray();
    }
}

using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using StalkerModLauncher.Models;
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
        if (directories.Count == 1)
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

    private void Window_OnSourceInitialized(object? sender, EventArgs e)
    {
        var handle = new WindowInteropHelper(this).Handle;
        if (handle == IntPtr.Zero)
        {
            return;
        }

        var enabled = 1;
        _ = DwmSetWindowAttribute(handle, 20, ref enabled, sizeof(int));
        _ = DwmSetWindowAttribute(handle, 19, ref enabled, sizeof(int));

        var captionColor = ToColorRef(0x0F, 0x11, 0x0D);
        var borderColor = ToColorRef(0x4A, 0x4E, 0x3A);
        var textColor = ToColorRef(0xF0, 0xE8, 0xC8);
        _ = DwmSetWindowAttribute(handle, 35, ref captionColor, sizeof(int));
        _ = DwmSetWindowAttribute(handle, 34, ref borderColor, sizeof(int));
        _ = DwmSetWindowAttribute(handle, 36, ref textColor, sizeof(int));
    }

    private static int ToColorRef(byte red, byte green, byte blue) =>
        red | (green << 8) | (blue << 16);

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int attributeValue, int attributeSize);
}

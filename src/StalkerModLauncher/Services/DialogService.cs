using System.Diagnostics;
using Microsoft.Win32;
using System.Windows;

namespace StalkerModLauncher.Services;

public sealed class DialogService
{
    public string? PickFolder(string description, string? initialPath = null)
    {
        var dialog = new OpenFolderDialog
        {
            Title = description,
            InitialDirectory = Directory.Exists(initialPath) ? initialPath : string.Empty,
            Multiselect = false
        };

        return dialog.ShowDialog() == true ? dialog.FolderName : null;
    }

    public string? PickExecutable(string title, string? initialPath = null)
    {
        var dialog = new OpenFileDialog
        {
            Title = title,
            Filter = "Executable files (*.exe)|*.exe|All files (*.*)|*.*",
            InitialDirectory = Directory.Exists(initialPath) ? initialPath : string.Empty,
            CheckFileExists = true,
            Multiselect = false
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public bool Confirm(string title, string message)
    {
        return MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes;
    }

    public void ShowError(string title, string message)
    {
        MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
    }

    public void OpenFolder(string path)
    {
        if (!Directory.Exists(path))
        {
            throw new DirectoryNotFoundException(path);
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = $"\"{path}\"",
            UseShellExecute = false
        });
    }
}

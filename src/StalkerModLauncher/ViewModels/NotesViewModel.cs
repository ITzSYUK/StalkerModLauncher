using System.Windows.Input;
using StalkerModLauncher.Infrastructure;
using StalkerModLauncher.Models;
using StalkerModLauncher.Services;

namespace StalkerModLauncher.ViewModels;

public sealed class NotesViewModel : ObservableObject
{
    private readonly string _notesFile;
    private readonly DialogService _dialogService;
    private string _notesText;

    public NotesViewModel(ModProfile profile, AppPaths paths, DialogService dialogService)
    {
        var name = FileSystemSafety.SanitizeName(profile.Name);
        _notesFile = Path.Combine(paths.ConfigDirectory, $"notes-{name}.txt");
        _dialogService = dialogService;
        _notesText = LoadFromFile();

        OpenNotesFolderCommand = new RelayCommand(OpenNotesFolder);
    }

    public string NotesText
    {
        get => _notesText;
        set
        {
            if (SetProperty(ref _notesText, value))
            {
                SaveToFile();
            }
        }
    }

    public ICommand OpenNotesFolderCommand { get; }

    private string LoadFromFile()
    {
        try
        {
            return File.Exists(_notesFile)
                ? File.ReadAllText(_notesFile)
                : string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private void SaveToFile()
    {
        try
        {
            var dir = Path.GetDirectoryName(_notesFile);
            if (dir is not null)
            {
                Directory.CreateDirectory(dir);
            }

            File.WriteAllText(_notesFile, _notesText);
        }
        catch
        {
            // ignored
        }
    }

    private void OpenNotesFolder()
    {
        try
        {
            var dir = Path.GetDirectoryName(_notesFile);
            if (dir is not null)
            {
                Directory.CreateDirectory(dir);
                _dialogService.OpenFolder(dir);
            }
        }
        catch
        {
            // ignored
        }
    }
}

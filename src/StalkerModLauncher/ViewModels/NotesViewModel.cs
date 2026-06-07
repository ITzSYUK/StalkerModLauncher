using System.Windows.Input;
using StalkerModLauncher.Infrastructure;
using StalkerModLauncher.Models;
using StalkerModLauncher.Services;

namespace StalkerModLauncher.ViewModels;

public sealed class NotesViewModel : ObservableObject
{
    private readonly ModProfile _profile;
    private readonly ProfileNotesStore _notesStore;
    private readonly DialogService _dialogService;
    private string _notesText;

    public NotesViewModel(ModProfile profile, AppPaths paths, DialogService dialogService)
    {
        _profile = profile;
        _notesStore = new ProfileNotesStore(paths);
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
            return _notesStore.Load(_profile);
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
            _notesStore.Save(_profile, _notesText);
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
            Directory.CreateDirectory(_notesStore.NotesDirectory);
            _dialogService.OpenFolder(_notesStore.NotesDirectory);
        }
        catch
        {
            // ignored
        }
    }
}

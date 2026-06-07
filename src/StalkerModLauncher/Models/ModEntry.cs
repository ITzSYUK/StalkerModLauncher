using StalkerModLauncher.Infrastructure;

namespace StalkerModLauncher.Models;

public sealed class ModEntry : ObservableObject
{
    private string _id = Guid.NewGuid().ToString("N");
    private string _name = "New mod";
    private string _sourcePath = string.Empty;
    private bool _isEnabled = true;
    private bool _isLocked;
    private bool _hasOverlapsAbove;
    private int _order;
    private string _notes = string.Empty;

    public string Id
    {
        get => _id;
        set => SetProperty(ref _id, value);
    }

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public string SourcePath
    {
        get => _sourcePath;
        set => SetProperty(ref _sourcePath, value);
    }

    public bool IsEnabled
    {
        get => _isEnabled;
        set => SetProperty(ref _isEnabled, value);
    }

    public bool IsLocked
    {
        get => _isLocked;
        set => SetProperty(ref _isLocked, value);
    }

    public bool HasOverlapsAbove
    {
        get => _hasOverlapsAbove;
        set => SetProperty(ref _hasOverlapsAbove, value);
    }

    public int Order
    {
        get => _order;
        set => SetProperty(ref _order, value);
    }

    public string Notes
    {
        get => _notes;
        set => SetProperty(ref _notes, value);
    }
}

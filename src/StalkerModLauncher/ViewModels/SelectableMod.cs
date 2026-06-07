using StalkerModLauncher.Infrastructure;
using StalkerModLauncher.Services;

namespace StalkerModLauncher.ViewModels;

public sealed class SelectableMod : ObservableObject
{
    private bool _isSelected;

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string DetectedBy { get; set; } = string.Empty;

    public static SelectableMod FromDiscovered(DiscoveredMod m) => new()
    {
        Name = m.Name,
        Path = m.Path,
        DetectedBy = m.DetectedBy
    };
}

using StalkerModLauncher.Infrastructure;
using System.Text.Json.Serialization;

namespace StalkerModLauncher.Models;

public sealed class ModEntry : ObservableObject
{
    private string _id = Guid.NewGuid().ToString("N");
    private string _name = "New mod";
    private string _sourcePath = string.Empty;
    private bool _isEnabled = true;
    private bool _hasOverlapsAbove;
    private int _overwrittenFileCount;
    private int _overwrittenModCount;
    private bool _providesLaunchExecutable;
    private int _overwrittenConfigurationCount;
    private int _overwrittenBinaryCount;
    private string _overlayDetails = string.Empty;
    private int _order;

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

    [JsonIgnore]
    public bool HasOverlapsAbove
    {
        get => _hasOverlapsAbove;
        set => SetProperty(ref _hasOverlapsAbove, value);
    }

    [JsonIgnore]
    public int OverwrittenFileCount
    {
        get => _overwrittenFileCount;
        set
        {
            if (SetProperty(ref _overwrittenFileCount, value))
            {
                OnPropertyChanged(nameof(OverlaySummary));
                OnPropertyChanged(nameof(HasOverlayInfo));
            }
        }
    }

    [JsonIgnore]
    public int OverwrittenModCount
    {
        get => _overwrittenModCount;
        set
        {
            if (SetProperty(ref _overwrittenModCount, value))
            {
                OnPropertyChanged(nameof(OverlaySummary));
            }
        }
    }

    [JsonIgnore]
    public bool ProvidesLaunchExecutable
    {
        get => _providesLaunchExecutable;
        set
        {
            if (SetProperty(ref _providesLaunchExecutable, value))
            {
                OnPropertyChanged(nameof(OverlaySummary));
                OnPropertyChanged(nameof(HasOverlayInfo));
            }
        }
    }

    [JsonIgnore]
    public int OverwrittenConfigurationCount
    {
        get => _overwrittenConfigurationCount;
        set
        {
            if (SetProperty(ref _overwrittenConfigurationCount, value))
            {
                OnPropertyChanged(nameof(OverlaySummary));
            }
        }
    }

    [JsonIgnore]
    public int OverwrittenBinaryCount
    {
        get => _overwrittenBinaryCount;
        set
        {
            if (SetProperty(ref _overwrittenBinaryCount, value))
            {
                OnPropertyChanged(nameof(OverlaySummary));
            }
        }
    }

    [JsonIgnore]
    public string OverlayDetails
    {
        get => _overlayDetails;
        set => SetProperty(ref _overlayDetails, value);
    }

    [JsonIgnore]
    public bool HasOverlayInfo => OverwrittenFileCount > 0 || ProvidesLaunchExecutable;

    [JsonIgnore]
    public string OverlaySummary
    {
        get
        {
            var parts = new List<string>();
            if (OverwrittenFileCount > 0)
            {
                parts.Add($"Заменяет {OverwrittenFileCount:N0} {Pluralize(OverwrittenFileCount, "файл", "файла", "файлов")} из {OverwrittenModCount:N0} {Pluralize(OverwrittenModCount, "мода", "модов", "модов")}");
            }

            if (ProvidesLaunchExecutable)
            {
                parts.Add("предоставляет запускаемый бинарник");
            }

            return string.Join(" · ", parts);
        }
    }

    public int Order
    {
        get => _order;
        set => SetProperty(ref _order, value);
    }

    private static string Pluralize(int value, string one, string few, string many)
    {
        var absolute = Math.Abs(value) % 100;
        if (absolute is >= 11 and <= 19)
        {
            return many;
        }

        return (absolute % 10) switch
        {
            1 => one,
            >= 2 and <= 4 => few,
            _ => many
        };
    }
}

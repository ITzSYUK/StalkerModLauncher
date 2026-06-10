using System.Collections.ObjectModel;
using System.Text.Json.Serialization;
using StalkerModLauncher.Infrastructure;

namespace StalkerModLauncher.Models;

public sealed class ModProfile : ObservableObject
{
    private string _id = Guid.NewGuid().ToString("N");
    private string _name = "New profile";
    private string _description = string.Empty;
    private bool _isEnabled = true;
    private bool _isStandalone;
    private string _launchArguments = "-nointro";
    private string _executableRelativePath = @"bin\xr_3da.exe";
    private double _totalPlaytimeSeconds;
    private DateTime? _lastPlayedAt;
    private string _workspacePath = string.Empty;
    private string _workingDirectoryRelative = string.Empty;
    private string _configNotes = string.Empty;
    private string _gameInstallPath = string.Empty;
    private bool _isRunning;
    private ObservableCollection<ModEntry> _mods = new();

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

    public string Description
    {
        get => _description;
        set => SetProperty(ref _description, value);
    }

    public bool IsEnabled
    {
        get => _isEnabled;
        set => SetProperty(ref _isEnabled, value);
    }

    public bool IsStandalone
    {
        get => _isStandalone;
        set => SetProperty(ref _isStandalone, value);
    }

    public string LaunchArguments
    {
        get => _launchArguments;
        set => SetProperty(ref _launchArguments, value);
    }

    public string ExecutableRelativePath
    {
        get => _executableRelativePath;
        set => SetProperty(ref _executableRelativePath, value);
    }

    public double TotalPlaytimeSeconds
    {
        get => _totalPlaytimeSeconds;
        set
        {
            if (SetProperty(ref _totalPlaytimeSeconds, value))
            {
                OnPropertyChanged(nameof(PlaytimeDisplay));
            }
        }
    }

    public DateTime? LastPlayedAt
    {
        get => _lastPlayedAt;
        set
        {
            if (SetProperty(ref _lastPlayedAt, value))
            {
                OnPropertyChanged(nameof(LastPlayedDisplay));
            }
        }
    }

    [JsonIgnore]
    public string PlaytimeDisplay
    {
        get
        {
            var total = TimeSpan.FromSeconds(_totalPlaytimeSeconds);
            if (total.TotalHours >= 1)
            {
                return $"{(int)total.TotalHours} ч {total.Minutes} мин";
            }

            if (total.TotalMinutes >= 1)
            {
                return $"{(int)total.TotalMinutes} мин";
            }

            return $"{total.TotalSeconds:N0} сек";
        }
    }

    [JsonIgnore]
    public string LastPlayedDisplay => _lastPlayedAt?.ToString("g") ?? "—";

    public string WorkspacePath
    {
        get => _workspacePath;
        set => SetProperty(ref _workspacePath, value);
    }

    public string WorkingDirectoryRelative
    {
        get => _workingDirectoryRelative;
        set => SetProperty(ref _workingDirectoryRelative, value);
    }

    public string ConfigNotes
    {
        get => _configNotes;
        set => SetProperty(ref _configNotes, value);
    }

    public string GameInstallPath
    {
        get => _gameInstallPath;
        set => SetProperty(ref _gameInstallPath, value);
    }

    [JsonIgnore]
    public bool IsRunning
    {
        get => _isRunning;
        set => SetProperty(ref _isRunning, value);
    }

    public ObservableCollection<ModEntry> Mods
    {
        get => _mods;
        set => SetProperty(ref _mods, value);
    }
}

using System.Collections.ObjectModel;
using System.Windows.Input;
using StalkerModLauncher.Infrastructure;
using StalkerModLauncher.Models;
using StalkerModLauncher.Services;

namespace StalkerModLauncher.ViewModels;

public sealed class ProfileCreationViewModel : ObservableObject
{
    private readonly DialogService _dialogService;
    private int _step = 1;
    private string _name = "Новый профиль";
    private bool _isStandalone;
    private string _gamePath = string.Empty;
    private string _executableRelativePath = @"bin\xr_3da.exe";
    private string _launchArguments = "-nointro";
    private string _message = "Выберите тип профиля и задайте понятное название.";

    public ProfileCreationViewModel(DialogService dialogService)
    {
        _dialogService = dialogService;
        NextCommand = new RelayCommand(Next, () => Step < 3);
        BackCommand = new RelayCommand(Back, () => Step > 1);
        FinishCommand = new RelayCommand(Finish, () => Step == 3);
        BrowseGameCommand = new RelayCommand(BrowseGame);
        BrowseStandaloneCommand = new RelayCommand(BrowseStandalone);
        AddModCommand = new RelayCommand(AddMod, () => !IsStandalone || Mods.Count == 0);
        RemoveModCommand = new RelayCommand(parameter => RemoveMod(parameter as ModEntry), parameter => parameter is ModEntry);
        BrowseExecutableCommand = new RelayCommand(BrowseExecutable);
    }

    public event EventHandler<ModProfile>? Completed;

    public ObservableCollection<ModEntry> Mods { get; } = new();

    public int Step
    {
        get => _step;
        private set
        {
            if (SetProperty(ref _step, value))
            {
                OnPropertyChanged(nameof(StepTitle));
                OnPropertyChanged(nameof(IsStepOne));
                OnPropertyChanged(nameof(IsStepTwo));
                OnPropertyChanged(nameof(IsStepThree));
                RaiseCommandStates();
            }
        }
    }

    public string StepTitle => Step switch
    {
        1 => "1. Тип профиля",
        2 => "2. Источники файлов",
        _ => "3. Запуск"
    };

    public bool IsStepOne => Step == 1;
    public bool IsStepTwo => Step == 2;
    public bool IsStepThree => Step == 3;

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public bool IsStandalone
    {
        get => _isStandalone;
        set
        {
            if (SetProperty(ref _isStandalone, value))
            {
                OnPropertyChanged(nameof(ProfileTypeDescription));
                OnPropertyChanged(nameof(IsOverlay));
                OnPropertyChanged(nameof(StandalonePath));
                ((RelayCommand)AddModCommand).RaiseCanExecuteChanged();
                if (value && Mods.Count > 1)
                {
                    while (Mods.Count > 1)
                    {
                        Mods.RemoveAt(Mods.Count - 1);
                    }
                }
            }
        }
    }

    public bool IsOverlay
    {
        get => !IsStandalone;
        set
        {
            if (value)
            {
                IsStandalone = false;
            }
        }
    }

    public string StandalonePath => Mods.FirstOrDefault()?.SourcePath ?? string.Empty;

    public string ProfileTypeDescription => IsStandalone
        ? "Автономный мод уже содержит движок и запускается прямо из своей папки."
        : "Обычный профиль объединяет базовую игру и включённые модификации в изолированном workspace.";

    public string GamePath
    {
        get => _gamePath;
        set => SetProperty(ref _gamePath, value);
    }

    public string ExecutableRelativePath
    {
        get => _executableRelativePath;
        set => SetProperty(ref _executableRelativePath, value);
    }

    public string LaunchArguments
    {
        get => _launchArguments;
        set => SetProperty(ref _launchArguments, value);
    }

    public string Message
    {
        get => _message;
        private set => SetProperty(ref _message, value);
    }

    public ICommand NextCommand { get; }
    public ICommand BackCommand { get; }
    public ICommand FinishCommand { get; }
    public ICommand BrowseGameCommand { get; }
    public ICommand BrowseStandaloneCommand { get; }
    public ICommand AddModCommand { get; }
    public ICommand RemoveModCommand { get; }
    public ICommand BrowseExecutableCommand { get; }

    private void Next()
    {
        if (!ValidateCurrentStep())
        {
            return;
        }

        Step++;
        Message = Step == 2
            ? "Укажите папки, из которых лаунчер возьмёт файлы."
            : "Проверьте бинарник. Моды ниже в списке имеют более высокий приоритет.";

        if (Step == 3)
        {
            AutoDetectExecutable();
        }
    }

    private void Back()
    {
        Step--;
        Message = Step == 1
            ? "Выберите тип профиля и задайте понятное название."
            : "Укажите папки, из которых лаунчер возьмёт файлы.";
    }

    private void Finish()
    {
        if (!ValidateCurrentStep())
        {
            return;
        }

        var profile = new ModProfile
        {
            Name = Name.Trim(),
            Description = IsStandalone ? "Автономный мод" : "Мод поверх базовой игры",
            IsStandalone = IsStandalone,
            GameInstallPath = IsStandalone ? string.Empty : GamePath.Trim(),
            ExecutableRelativePath = ExecutableRelativePath.Trim(),
            LaunchArguments = LaunchArguments.Trim()
        };

        foreach (var mod in Mods)
        {
            profile.Mods.Add(new ModEntry
            {
                Name = mod.Name,
                SourcePath = mod.SourcePath,
                IsEnabled = true,
                Order = profile.Mods.Count + 1
            });
        }

        Completed?.Invoke(this, profile);
    }

    private bool ValidateCurrentStep()
    {
        if (Step == 1 && string.IsNullOrWhiteSpace(Name))
        {
            Message = "Введите название профиля.";
            return false;
        }

        if (Step == 2)
        {
            if (!IsStandalone && !Directory.Exists(GamePath))
            {
                Message = "Выберите существующую папку базовой игры.";
                return false;
            }

            if (IsStandalone && (Mods.Count != 1 || !Directory.Exists(Mods[0].SourcePath)))
            {
                Message = "Для автономного профиля выберите одну папку мода.";
                return false;
            }
        }

        if (Step == 3)
        {
            try
            {
                FileSystemSafety.EnsureRelativePath(ExecutableRelativePath, "Бинарник запуска");
            }
            catch (Exception ex)
            {
                Message = ex.Message;
                return false;
            }
        }

        return true;
    }

    private void BrowseGame()
    {
        var selected = _dialogService.PickFolder("Выберите папку базовой игры", GamePath);
        if (selected is not null)
        {
            GamePath = selected;
        }
    }

    private void BrowseStandalone()
    {
        var selected = _dialogService.PickFolder("Выберите папку автономной игры или мода", StandalonePath);
        if (selected is null)
        {
            return;
        }

        Mods.Clear();
        Mods.Add(new ModEntry
        {
            Name = Path.GetFileName(selected.TrimEnd(Path.DirectorySeparatorChar)),
            SourcePath = selected,
            Order = 1
        });
        OnPropertyChanged(nameof(StandalonePath));
        ((RelayCommand)AddModCommand).RaiseCanExecuteChanged();
    }

    private void AddMod()
    {
        var selected = _dialogService.PickFolder(IsStandalone ? "Выберите папку автономного мода" : "Выберите папку мода");
        if (selected is null || Mods.Any(mod => FileSystemSafety.IsSameDirectory(mod.SourcePath, selected)))
        {
            return;
        }

        Mods.Add(new ModEntry
        {
            Name = Path.GetFileName(selected.TrimEnd(Path.DirectorySeparatorChar)),
            SourcePath = selected,
            Order = Mods.Count + 1
        });
        OnPropertyChanged(nameof(StandalonePath));
        ((RelayCommand)AddModCommand).RaiseCanExecuteChanged();
    }

    private void RemoveMod(ModEntry? mod)
    {
        if (mod is null)
        {
            return;
        }

        Mods.Remove(mod);
        for (var index = 0; index < Mods.Count; index++)
        {
            Mods[index].Order = index + 1;
        }
        OnPropertyChanged(nameof(StandalonePath));
        ((RelayCommand)AddModCommand).RaiseCanExecuteChanged();
    }

    private void BrowseExecutable()
    {
        var initial = Mods.LastOrDefault()?.SourcePath;
        if (!Directory.Exists(initial))
        {
            initial = GamePath;
        }

        var selected = _dialogService.PickExecutable("Выберите запускаемый файл", initial);
        if (selected is null)
        {
            return;
        }

        var roots = (IsStandalone ? Enumerable.Empty<string>() : [GamePath])
            .Concat(Mods.Select(mod => mod.SourcePath))
            .Where(Directory.Exists);
        foreach (var root in roots)
        {
            var relative = Path.GetRelativePath(root, selected);
            if (!relative.StartsWith("..", StringComparison.Ordinal) && !Path.IsPathRooted(relative))
            {
                ExecutableRelativePath = relative;
                return;
            }
        }

        Message = "Выбранный бинарник должен находиться внутри папки игры или одного из модов.";
    }

    private void AutoDetectExecutable()
    {
        var roots = (IsStandalone ? Enumerable.Empty<string>() : [GamePath])
            .Concat(Mods.Select(mod => mod.SourcePath))
            .Where(Directory.Exists)
            .ToArray();
        var found = roots
            .SelectMany(root => Directory.EnumerateFiles(root, "*.exe", SearchOption.AllDirectories)
                .Select(path => new { Root = root, Path = path }))
            .OrderBy(candidate => Path.GetFileName(candidate.Path).Contains("xrEngine", StringComparison.OrdinalIgnoreCase) ? 0
                : Path.GetFileName(candidate.Path).Contains("xr_3da", StringComparison.OrdinalIgnoreCase) ? 1
                : 2)
            .FirstOrDefault();
        if (found is not null)
        {
            ExecutableRelativePath = Path.GetRelativePath(found.Root, found.Path);
        }
    }

    private void RaiseCommandStates()
    {
        ((RelayCommand)NextCommand).RaiseCanExecuteChanged();
        ((RelayCommand)BackCommand).RaiseCanExecuteChanged();
        ((RelayCommand)FinishCommand).RaiseCanExecuteChanged();
    }
}

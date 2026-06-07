using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using StalkerModLauncher.Infrastructure;
using StalkerModLauncher.Models;

namespace StalkerModLauncher.ViewModels;

public sealed class ScreenshotsViewModel : ObservableObject
{
    private BitmapImage? _selectedScreenshot;
    private bool _isFullScreen;
    private int _selectedIndex = -1;

    public ScreenshotsViewModel(ModProfile profile, string defaultGamePath)
    {
        OpenScreenshotCommand = new RelayCommand(param =>
        {
            if (param is ScreenshotItem item)
            {
                var index = Screenshots.IndexOf(item);
                if (index >= 0)
                {
                    OpenScreenshot(index);
                }
            }
        });

        CloseFullScreenCommand = new RelayCommand(CloseFullScreen);
        GoPreviousCommand = new RelayCommand(_ => GoPrevious(), _ => CanGoPrevious);
        GoNextCommand = new RelayCommand(_ => GoNext(), _ => CanGoNext);

        LoadScreenshots(profile, defaultGamePath);
    }

    public ObservableCollection<ScreenshotItem> Screenshots { get; } = new();

    public BitmapImage? SelectedScreenshot
    {
        get => _selectedScreenshot;
        set => SetProperty(ref _selectedScreenshot, value);
    }

    public bool IsFullScreen
    {
        get => _isFullScreen;
        set
        {
            if (SetProperty(ref _isFullScreen, value))
            {
                OnPropertyChanged(nameof(ShowThumbnailGrid));
                OnPropertyChanged(nameof(ShowFullScreenView));
                OnPropertyChanged(nameof(HasNoScreenshots));
                RefreshNavigationState();
            }
        }
    }

    public bool ShowThumbnailGrid => !_isFullScreen && Screenshots.Count > 0;
    public bool ShowFullScreenView => _isFullScreen;
    public bool HasNoScreenshots => Screenshots.Count == 0 && !_isFullScreen;
    public bool CanGoPrevious => _isFullScreen && _selectedIndex > 0;
    public bool CanGoNext => _isFullScreen && _selectedIndex < Screenshots.Count - 1;

    public ICommand OpenScreenshotCommand { get; }
    public ICommand CloseFullScreenCommand { get; }
    public ICommand GoPreviousCommand { get; }
    public ICommand GoNextCommand { get; }

    public void OpenScreenshot(int index)
    {
        if (index < 0 || index >= Screenshots.Count)
        {
            return;
        }

        _selectedIndex = index;
        SelectedScreenshot = Screenshots[index].Source;
        if (!_isFullScreen)
        {
            IsFullScreen = true;
        }
        else
        {
            RefreshNavigationState();
        }
    }

    public void CloseFullScreen()
    {
        IsFullScreen = false;
    }

    public void GoPrevious()
    {
        if (_selectedIndex > 0)
        {
            OpenScreenshot(_selectedIndex - 1);
        }
    }

    public void GoNext()
    {
        if (_selectedIndex < Screenshots.Count - 1)
        {
            OpenScreenshot(_selectedIndex + 1);
        }
    }

    public void HandleKeyDown(System.Windows.Input.Key key)
    {
        if (!_isFullScreen)
        {
            return;
        }

        switch (key)
        {
            case Key.Left:
                GoPrevious();
                break;
            case Key.Right:
                GoNext();
                break;
            case Key.Escape:
                CloseFullScreen();
                break;
        }
    }

    private void RefreshNavigationState()
    {
        OnPropertyChanged(nameof(CanGoPrevious));
        OnPropertyChanged(nameof(CanGoNext));
        ((RelayCommand)GoPreviousCommand).RaiseCanExecuteChanged();
        ((RelayCommand)GoNextCommand).RaiseCanExecuteChanged();
    }

    private void LoadScreenshots(ModProfile profile, string defaultGamePath)
    {
        var searchPaths = new List<string>();

        var gamePath = !string.IsNullOrWhiteSpace(profile.GameInstallPath)
            ? profile.GameInstallPath
            : defaultGamePath;

        if (!string.IsNullOrWhiteSpace(gamePath))
        {
            searchPaths.Add(Path.Combine(gamePath, "userdata", "screenshots"));
            searchPaths.Add(Path.Combine(gamePath, "appdata", "screenshots"));
        }

        var modRoot = profile.Mods.FirstOrDefault(m => m.IsEnabled && Directory.Exists(m.SourcePath))?.SourcePath;
        if (modRoot is not null)
        {
            var appdata = Path.Combine(modRoot, "_appdata_", "screenshots");
            if (Directory.Exists(appdata))
            {
                searchPaths.Add(appdata);
            }

            var binAppdata = Path.Combine(modRoot, "bin", "_appdata_", "screenshots");
            if (Directory.Exists(binAppdata))
            {
                searchPaths.Add(binAppdata);
            }

            var modAppdata = Path.Combine(modRoot, "appdata", "screenshots");
            if (Directory.Exists(modAppdata))
            {
                searchPaths.Add(modAppdata);
            }
        }

        if (!string.IsNullOrWhiteSpace(profile.WorkspacePath))
        {
            searchPaths.Add(Path.Combine(profile.WorkspacePath, "userdata", "screenshots"));
        }

        Screenshots.Clear();
        foreach (var dir in searchPaths.Distinct())
        {
            if (!Directory.Exists(dir))
            {
                continue;
            }

            foreach (var file in Directory.EnumerateFiles(dir, "*.*")
                         .Where(f => f.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
                                  || f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
                                  || f.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase)
                                  || f.EndsWith(".bmp", StringComparison.OrdinalIgnoreCase))
                         .OrderBy(f => f))
            {
                Screenshots.Add(new ScreenshotItem(file));
            }
        }

        OnPropertyChanged(nameof(HasNoScreenshots));
        OnPropertyChanged(nameof(ShowThumbnailGrid));
    }
}

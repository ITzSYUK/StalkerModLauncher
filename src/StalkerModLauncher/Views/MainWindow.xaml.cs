using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using StalkerModLauncher.Services;
using StalkerModLauncher.ViewModels;

namespace StalkerModLauncher.Views;

public partial class MainWindow : Window
{
    private bool _isClosingAfterCleanup;
    private bool _initialInterfaceReady;
    private bool _isSwitchingToClassic;
    private PdaWindow? _pdaWindow;
    private readonly WindowNavigationService _navigation;
    private readonly WindowSystemIntegrationService _windowSystemIntegration;

    public MainWindow(
        MainViewModel viewModel,
        WindowNavigationService navigation,
        WindowSystemIntegrationService windowSystemIntegration)
    {
        InitializeComponent();
        _navigation = navigation;
        _windowSystemIntegration = windowSystemIntegration;
        viewModel.ActivityLog.PropertyChanged += ActivityLog_PropertyChanged;
        viewModel.PropertyChanged += ViewModel_PropertyChanged;
        viewModel.ProfileCreationRequested += ViewModel_ProfileCreationRequested;
        DataContext = viewModel;
    }

    private void ViewModel_ProfileCreationRequested(object? sender, EventArgs e)
    {
        if (ViewModel is null || _pdaWindow is not null)
        {
            return;
        }

        _navigation.ShowProfileCreation(this, ViewModel);
    }

    private void ActivityLog_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ActivityLogViewModel.IsVisible))
        {
            var row = RootGrid.RowDefinitions[2];
            var vm = (MainViewModel)DataContext;
            BindingOperations.SetBinding(row, RowDefinition.HeightProperty,
                new Binding(nameof(ActivityLogViewModel.RowHeight)) { Source = vm.ActivityLog });
        }
    }

    private MainViewModel? ViewModel => DataContext as MainViewModel;

    private void Window_OnSourceInitialized(object? sender, EventArgs e)
    {
        _windowSystemIntegration.Initialize(this);
    }

    public async Task<bool> ShowInitialInterfaceAsync()
    {
        if (ViewModel is null)
        {
            Show();
            return false;
        }

        await ViewModel.Initialization;
        _initialInterfaceReady = true;
        if (ViewModel.IsPdaInterfaceEnabled)
        {
            ShowPdaWindow();
            return true;
        }

        Show();
        return false;
    }

    private void EditProfileButton_OnClick(object sender, RoutedEventArgs e)
    {
        var settingsVm = ViewModel?.CreateProfileSettingsViewModel();
        if (settingsVm is null)
        {
            return;
        }

        _navigation.ShowProfileSettings(this, settingsVm);
    }

    private void ScreenshotsButton_OnClick(object sender, RoutedEventArgs e)
    {
        var profile = ViewModel?.SelectedProfile;
        if (profile is null)
        {
            return;
        }

        _navigation.ShowScreenshots(this, profile);
    }

    private void ModCatalogButton_OnClick(object sender, RoutedEventArgs e)
    {
        _navigation.ShowModCatalog(this);
    }

    private void ProfileHealthButton_OnClick(object sender, RoutedEventArgs e)
    {
        var profile = ViewModel?.SelectedProfile;
        if (profile is null)
        {
            return;
        }

        _navigation.ShowProfileHealth(this, profile, ViewModel is null ? null : ViewModel.AppendLog);
    }

    private async void Title_MouseDown(object sender, MouseButtonEventArgs e)
    {
        await _navigation.ShowAboutAsync(this);
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!_initialInterfaceReady || e.PropertyName != nameof(MainViewModel.IsPdaInterfaceEnabled) || ViewModel is null)
        {
            return;
        }

        Dispatcher.BeginInvoke(() =>
        {
            if (ViewModel.IsPdaInterfaceEnabled)
            {
                ShowPdaWindow();
            }
            else
            {
                if (_pdaWindow is not null)
                {
                    _isSwitchingToClassic = true;
                    _pdaWindow.Close();
                }
                else if (!IsVisible)
                {
                    Show();
                    Activate();
                }
            }
        });
    }

    private void ShowPdaWindow()
    {
        if (ViewModel is null || _pdaWindow is not null)
        {
            return;
        }

        _pdaWindow = new PdaWindow(ViewModel, _navigation);
        _pdaWindow.Closed += (_, _) =>
        {
            _pdaWindow = null;
            if (_isClosingAfterCleanup)
            {
                return;
            }

            if (_isSwitchingToClassic)
            {
                _isSwitchingToClassic = false;
                Show();
                Activate();
                return;
            }

            Close();
        };
        _pdaWindow.Show();
        Hide();
    }

    private async void Window_OnClosing(object? sender, CancelEventArgs e)
    {
        if (_isClosingAfterCleanup)
        {
            return;
        }

        e.Cancel = true;
        if (ViewModel is not null)
        {
            await ViewModel.CleanupAsync();
        }

        _isClosingAfterCleanup = true;
        _pdaWindow?.Close();
        Close();
    }

}

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
        viewModel.ProfileCreationRequested += ViewModel_ProfileCreationRequested;
        DataContext = viewModel;
    }

    private void ViewModel_ProfileCreationRequested(object? sender, EventArgs e)
    {
        if (ViewModel is null)
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
        _windowSystemIntegration.Initialize(this, ToggleNotesOverlay);
    }

    private void ToggleNotesOverlay()
    {
        var profile = ViewModel?.SelectedProfile;
        if (profile is null)
        {
            return;
        }

        _navigation.ToggleNotesOverlay(profile);
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

    private void NotesButton_OnClick(object sender, RoutedEventArgs e)
    {
        var profile = ViewModel?.SelectedProfile;
        if (profile is null)
        {
            return;
        }

        _navigation.ShowNotes(this, profile);
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

    private void ProfileHealthButton_OnClick(object sender, RoutedEventArgs e)
    {
        var profile = ViewModel?.SelectedProfile;
        if (profile is null)
        {
            return;
        }

        _navigation.ShowProfileHealth(this, profile);
    }

    private async void Title_MouseDown(object sender, MouseButtonEventArgs e)
    {
        await _navigation.ShowAboutAsync(this);
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
        Close();
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        _windowSystemIntegration.Dispose();
    }
}

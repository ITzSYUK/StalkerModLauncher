using System.Windows;
using StalkerModLauncher.Services;
using StalkerModLauncher.ViewModels;

namespace StalkerModLauncher.Views;

public partial class ProfileSettingsWindow : Window
{
    private readonly ProfileSettingsViewModel _viewModel;

    public ProfileSettingsWindow(ProfileSettingsViewModel viewModel)
    {
        _viewModel = viewModel;
        InitializeComponent();
        DataContext = viewModel;
    }

    private void Window_OnSourceInitialized(object? sender, EventArgs e)
    {
        WindowSystemIntegrationService.Initialize(this);
    }

    private async void CloseButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (await _viewModel.TrySaveAsync())
        {
            Close();
        }
    }

}

using System.Windows;
using StalkerModLauncher.Services;
using StalkerModLauncher.ViewModels;

namespace StalkerModLauncher.Views;

public partial class LauncherSettingsWindow : Window
{
    public LauncherSettingsWindow(LauncherSettingsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void Window_OnSourceInitialized(object? sender, EventArgs e) =>
        WindowSystemIntegrationService.Initialize(this);

    private void SettingsView_OnCompleted(object? sender, EventArgs e) => Close();
}

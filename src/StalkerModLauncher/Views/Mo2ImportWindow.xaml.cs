using System.Windows;
using StalkerModLauncher.Services;
using StalkerModLauncher.ViewModels;

namespace StalkerModLauncher.Views;

public partial class Mo2ImportWindow : Window
{
    private readonly WindowSystemIntegrationService _windowSystemIntegration;

    public Mo2ImportWindow(
        Mo2ImportViewModel viewModel,
        WindowSystemIntegrationService windowSystemIntegration)
    {
        InitializeComponent();
        _windowSystemIntegration = windowSystemIntegration;
        DataContext = viewModel;
        viewModel.Completed += (_, _) => DialogResult = true;
    }

    private void Window_OnSourceInitialized(object? sender, EventArgs e) =>
        _windowSystemIntegration.Initialize(this);

    private void CancelButton_OnClick(object sender, RoutedEventArgs e) => DialogResult = false;
}

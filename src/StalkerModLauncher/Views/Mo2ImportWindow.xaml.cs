using System.Windows;
using StalkerModLauncher.Services;
using StalkerModLauncher.ViewModels;

namespace StalkerModLauncher.Views;

public partial class Mo2ImportWindow : Window
{
    public Mo2ImportWindow(Mo2ImportViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.Completed += (_, _) => DialogResult = true;
    }

    private void Window_OnSourceInitialized(object? sender, EventArgs e) =>
        WindowSystemIntegrationService.Initialize(this);

    private void CancelButton_OnClick(object sender, RoutedEventArgs e) => DialogResult = false;
}

using System.Windows;
using StalkerModLauncher.Services;
using StalkerModLauncher.ViewModels;

namespace StalkerModLauncher.Views;

public partial class ProfileHealthWindow : Window
{
    public ProfileHealthWindow(ProfileHealthViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    protected override void OnClosed(EventArgs e)
    {
        (DataContext as IDisposable)?.Dispose();
        base.OnClosed(e);
    }

    private void Window_OnSourceInitialized(object? sender, EventArgs e)
    {
        WindowSystemIntegrationService.Initialize(this);
    }
}

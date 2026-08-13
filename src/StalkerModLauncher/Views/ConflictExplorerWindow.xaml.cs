using System.Windows;
using StalkerModLauncher.Services;
using StalkerModLauncher.ViewModels;

namespace StalkerModLauncher.Views;

public partial class ConflictExplorerWindow : Window
{
    private readonly WindowSystemIntegrationService _windowSystemIntegrationService;

    public ConflictExplorerWindow(
        ConflictExplorerViewModel viewModel,
        WindowSystemIntegrationService windowSystemIntegrationService)
    {
        InitializeComponent();
        DataContext = viewModel;
        _windowSystemIntegrationService = windowSystemIntegrationService;
        Closed += (_, _) => viewModel.Dispose();
    }

    private void Window_OnSourceInitialized(object? sender, EventArgs e)
    {
        _windowSystemIntegrationService.Initialize(this);
    }

    private void ContentView_OnCloseRequested(object? sender, EventArgs e) => Close();
}

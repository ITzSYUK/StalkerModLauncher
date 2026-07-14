using System.Windows;
using StalkerModLauncher.Services;

namespace StalkerModLauncher.Views;

public partial class UpdateAvailableWindow : Window
{
    private readonly WindowSystemIntegrationService _windowSystemIntegration;

    public UpdateAvailableWindow(
        string currentVersion,
        string latestVersion,
        WindowSystemIntegrationService windowSystemIntegration)
    {
        InitializeComponent();
        _windowSystemIntegration = windowSystemIntegration;
        CurrentVersionTextBlock.Text = currentVersion;
        LatestVersionTextBlock.Text = latestVersion;
        SourceInitialized += (_, _) => _windowSystemIntegration.Initialize(this);
    }

    private void LaterButton_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void DownloadButton_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }
}

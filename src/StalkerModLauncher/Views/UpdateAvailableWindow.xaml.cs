using System.Windows;
using StalkerModLauncher.Services;

namespace StalkerModLauncher.Views;

public partial class UpdateAvailableWindow : Window
{
    public UpdateAvailableWindow(
        string currentVersion,
        string latestVersion)
    {
        InitializeComponent();
        CurrentVersionTextBlock.Text = currentVersion;
        LatestVersionTextBlock.Text = latestVersion;
        SourceInitialized += (_, _) => WindowSystemIntegrationService.Initialize(this);
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

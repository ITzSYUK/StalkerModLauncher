using System.Windows;
using StalkerModLauncher.Services;

namespace StalkerModLauncher.Views;

public partial class ModArchiveInstalledWindow : Window
{
    public ModArchiveInstalledWindow(string modName, string modPath, string details)
    {
        InitializeComponent();
        ModNameTextBlock.Text = modName;
        ModPathTextBlock.Text = modPath;
        DetailsTextBlock.Text = details;
    }

    private void Window_OnSourceInitialized(object? sender, EventArgs e)
    {
        WindowSystemIntegrationService.Initialize(this);
    }
}

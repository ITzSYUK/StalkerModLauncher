using System.Windows;
using System.Windows.Controls;
using StalkerModLauncher.ViewModels;

namespace StalkerModLauncher.Views.Controls;

public partial class LauncherSettingsView : UserControl
{
    public LauncherSettingsView()
    {
        InitializeComponent();
    }

    public event EventHandler? Saved;
    public event EventHandler? Cancelled;

    private async void SaveButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is LauncherSettingsViewModel viewModel && await viewModel.TrySaveAsync())
        {
            Saved?.Invoke(this, EventArgs.Empty);
        }
    }

    private void CancelButton_OnClick(object sender, RoutedEventArgs e) =>
        Cancelled?.Invoke(this, EventArgs.Empty);
}

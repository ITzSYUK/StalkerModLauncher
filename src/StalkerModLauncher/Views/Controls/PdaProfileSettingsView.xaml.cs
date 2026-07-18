using System.Windows;
using System.Windows.Controls;
using StalkerModLauncher.ViewModels;

namespace StalkerModLauncher.Views.Controls;

public partial class PdaProfileSettingsView : UserControl
{
    public PdaProfileSettingsView()
    {
        InitializeComponent();
    }

    public event EventHandler? Saved;

    private async void SaveButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is ProfileSettingsViewModel viewModel && await viewModel.TrySaveAsync())
        {
            Saved?.Invoke(this, EventArgs.Empty);
        }
    }
}

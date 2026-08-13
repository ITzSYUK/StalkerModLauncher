using System.Windows;
using System.Windows.Controls;

namespace StalkerModLauncher.Views.Controls;

public partial class PdaMo2ImportView : UserControl
{
    public PdaMo2ImportView()
    {
        InitializeComponent();
    }

    public event EventHandler? Cancelled;

    private void CancelButton_OnClick(object sender, RoutedEventArgs e) =>
        Cancelled?.Invoke(this, EventArgs.Empty);
}

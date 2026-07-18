using System.Windows.Controls;
using System.Windows.Input;
using StalkerModLauncher.ViewModels;

namespace StalkerModLauncher.Views.Controls;

public partial class PdaScreenshotsView : UserControl
{
    public PdaScreenshotsView()
    {
        InitializeComponent();
        Focusable = true;
    }

    private void PdaScreenshotsView_OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is ScreenshotsViewModel viewModel)
        {
            viewModel.HandleKeyDown(e.Key);
        }
    }

    private void FullScreenGrid_OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (DataContext is not ScreenshotsViewModel viewModel)
        {
            return;
        }

        if (e.Delta > 0) viewModel.GoPrevious(); else viewModel.GoNext();
        e.Handled = true;
    }

    private void FullScreenImage_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2 && DataContext is ScreenshotsViewModel viewModel)
        {
            viewModel.CopySelectedScreenshot();
            e.Handled = true;
        }
    }

    private void OpenExternalFullScreenButton_OnClick(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is not ScreenshotsViewModel viewModel || viewModel.SelectedScreenshot is null)
        {
            return;
        }

        var window = new ScreenshotFullscreenWindow(viewModel)
        {
            Owner = System.Windows.Window.GetWindow(this)
        };
        window.ShowDialog();
    }

    private void CopyScreenshotMenuItem_OnClick(object sender, System.Windows.RoutedEventArgs e)
    {
        if (sender is not MenuItem menuItem || DataContext is not ScreenshotsViewModel viewModel)
        {
            return;
        }

        var item = menuItem.CommandParameter as ScreenshotItem
                   ?? ((menuItem.Parent as ContextMenu)?.PlacementTarget as System.Windows.FrameworkElement)?.DataContext as ScreenshotItem;
        if (item is not null)
        {
            viewModel.CopyScreenshot(item);
        }
        else
        {
            viewModel.CopySelectedScreenshot();
        }
    }
}

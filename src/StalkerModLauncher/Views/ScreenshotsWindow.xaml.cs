using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using StalkerModLauncher.Services;
using StalkerModLauncher.ViewModels;

namespace StalkerModLauncher.Views;

public partial class ScreenshotsWindow : Window
{
    public ScreenshotsWindow(ScreenshotsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    protected override void OnClosed(EventArgs e)
    {
        (DataContext as IDisposable)?.Dispose();
        DataContext = null;
        base.OnClosed(e);
    }

    private void ScreenshotsWindow_OnSourceInitialized(object? sender, EventArgs e)
    {
        WindowSystemIntegrationService.Initialize(this);
    }

    private void ScreenshotsWindow_OnKeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is ScreenshotsViewModel vm)
        {
            vm.HandleKeyDown(e.Key);
        }
    }

    private void FullScreenGrid_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (DataContext is ScreenshotsViewModel vm && vm.IsFullScreen)
        {
            if (e.Delta > 0)
            {
                vm.GoPrevious();
            }
            else
            {
                vm.GoNext();
            }

            e.Handled = true;
        }
    }

    private void FullScreenImage_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2 && DataContext is ScreenshotsViewModel vm)
        {
            vm.CopySelectedScreenshot();
            e.Handled = true;
        }
    }

    private void CopyScreenshotMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem menuItem || DataContext is not ScreenshotsViewModel vm)
        {
            return;
        }

        var item = menuItem.CommandParameter as ScreenshotItem
                   ?? ((menuItem.Parent as ContextMenu)?.PlacementTarget as FrameworkElement)?.DataContext as ScreenshotItem;
        if (item is not null)
        {
            vm.CopyScreenshot(item);
        }
    }

}

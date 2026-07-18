using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using StalkerModLauncher.ViewModels;

namespace StalkerModLauncher.Views;

public partial class ScreenshotFullscreenWindow : Window
{
    public ScreenshotFullscreenWindow(ScreenshotsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private ScreenshotsViewModel? ViewModel => DataContext as ScreenshotsViewModel;

    private void Window_OnKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Left:
                ViewModel?.GoPrevious();
                e.Handled = true;
                break;
            case Key.Right:
                ViewModel?.GoNext();
                e.Handled = true;
                break;
            case Key.Escape:
                Close();
                e.Handled = true;
                break;
        }
    }

    private void Window_OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (e.Delta > 0)
        {
            ViewModel?.GoPrevious();
        }
        else
        {
            ViewModel?.GoNext();
        }

        e.Handled = true;
    }

    private void Image_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            ViewModel?.CopySelectedScreenshot();
            e.Handled = true;
        }
    }

    private void CopyScreenshotMenuItem_OnClick(object sender, RoutedEventArgs e) =>
        ViewModel?.CopySelectedScreenshot();

    private void CloseButton_OnClick(object sender, RoutedEventArgs e) => Close();
}

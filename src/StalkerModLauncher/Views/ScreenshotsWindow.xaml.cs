using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
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
        base.OnClosed(e);
    }

    private void ScreenshotsWindow_OnSourceInitialized(object? sender, EventArgs e)
    {
        ApplyDarkWindowFrame();
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

    private void ApplyDarkWindowFrame()
    {
        var handle = new WindowInteropHelper(this).Handle;
        if (handle == IntPtr.Zero)
        {
            return;
        }

        var useDarkMode = 1;
        _ = DwmSetWindowAttribute(handle, DwmUseImmersiveDarkMode, ref useDarkMode, sizeof(int));
        _ = DwmSetWindowAttribute(handle, DwmUseImmersiveDarkModeBefore20H1, ref useDarkMode, sizeof(int));

        var captionColor = ToColorRef(0x0F, 0x11, 0x0D);
        var borderColor = ToColorRef(0x4A, 0x4E, 0x3A);
        var textColor = ToColorRef(0xF0, 0xE8, 0xC8);
        _ = DwmSetWindowAttribute(handle, DwmCaptionColor, ref captionColor, sizeof(int));
        _ = DwmSetWindowAttribute(handle, DwmBorderColor, ref borderColor, sizeof(int));
        _ = DwmSetWindowAttribute(handle, DwmTextColor, ref textColor, sizeof(int));
    }

    private static int ToColorRef(byte red, byte green, byte blue)
    {
        return red | (green << 8) | (blue << 16);
    }

    private const int DwmUseImmersiveDarkModeBefore20H1 = 19;
    private const int DwmUseImmersiveDarkMode = 20;
    private const int DwmBorderColor = 34;
    private const int DwmCaptionColor = 35;
    private const int DwmTextColor = 36;

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int attributeValue, int attributeSize);
}

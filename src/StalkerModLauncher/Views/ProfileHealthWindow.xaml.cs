using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using StalkerModLauncher.ViewModels;

namespace StalkerModLauncher.Views;

public partial class ProfileHealthWindow : Window
{
    public ProfileHealthWindow(ProfileHealthViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void Window_OnSourceInitialized(object? sender, EventArgs e)
    {
        var handle = new WindowInteropHelper(this).Handle;
        if (handle == IntPtr.Zero)
        {
            return;
        }

        var useDarkMode = 1;
        _ = DwmSetWindowAttribute(handle, 20, ref useDarkMode, sizeof(int));
        _ = DwmSetWindowAttribute(handle, 19, ref useDarkMode, sizeof(int));

        var captionColor = ToColorRef(0x0F, 0x11, 0x0D);
        var borderColor = ToColorRef(0x4A, 0x4E, 0x3A);
        var textColor = ToColorRef(0xF0, 0xE8, 0xC8);
        _ = DwmSetWindowAttribute(handle, 35, ref captionColor, sizeof(int));
        _ = DwmSetWindowAttribute(handle, 34, ref borderColor, sizeof(int));
        _ = DwmSetWindowAttribute(handle, 36, ref textColor, sizeof(int));
    }

    private static int ToColorRef(byte red, byte green, byte blue)
    {
        return red | (green << 8) | (blue << 16);
    }

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int attributeValue, int attributeSize);
}

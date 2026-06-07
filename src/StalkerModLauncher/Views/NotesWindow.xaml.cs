using System.Windows;
using System.Windows.Interop;
using System.Runtime.InteropServices;
using StalkerModLauncher.ViewModels;

namespace StalkerModLauncher.Views;

public partial class NotesWindow : Window
{
    public NotesWindow(NotesViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();
    }

    private NotesViewModel? ViewModel => DataContext as NotesViewModel;

    private void NotesWindow_OnLoaded(object sender, RoutedEventArgs e)
    {
        NotesTextBox.Focus();
    }

    private void Window_OnSourceInitialized(object? sender, EventArgs e)
    {
        ApplyDarkWindowFrame();
    }

    private void CloseButton_OnClick(object sender, RoutedEventArgs e)
    {
        Close();
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

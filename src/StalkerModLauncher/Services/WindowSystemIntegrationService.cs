using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace StalkerModLauncher.Services;

public sealed class WindowSystemIntegrationService : IDisposable
{
    private const int WhKeyboardLl = 13;
    private const int WmKeydown = 0x0100;
    private const int VkF2 = 0x71;
    private const int VkShift = 0x10;
    private const int DwmUseImmersiveDarkModeBefore20H1 = 19;
    private const int DwmUseImmersiveDarkMode = 20;
    private const int DwmBorderColor = 34;
    private const int DwmCaptionColor = 35;
    private const int DwmTextColor = 36;

    private nint _keyboardHook;
    private HookProc? _keyboardHookProc;
    private Action? _notesHotkeyAction;

    public void Initialize(Window window, Action notesHotkeyAction)
    {
        ApplyDarkWindowFrame(window);
        if (_keyboardHook != nint.Zero)
        {
            return;
        }

        _notesHotkeyAction = notesHotkeyAction;
        _keyboardHookProc = LowLevelKeyboardProc;
        _keyboardHook = SetWindowsHookEx(WhKeyboardLl, _keyboardHookProc, GetModuleHandle(null), 0);
    }

    public void Dispose()
    {
        if (_keyboardHook != nint.Zero)
        {
            _ = UnhookWindowsHookEx(_keyboardHook);
            _keyboardHook = nint.Zero;
        }

        _keyboardHookProc = null;
        _notesHotkeyAction = null;
    }

    private nint LowLevelKeyboardProc(int nCode, nint wParam, nint lParam)
    {
        if (nCode >= 0 &&
            wParam == WmKeydown &&
            Marshal.ReadInt32(lParam) == VkF2 &&
            (GetAsyncKeyState(VkShift) & 0x8000) != 0)
        {
            _notesHotkeyAction?.Invoke();
        }

        return CallNextHookEx(nint.Zero, nCode, wParam, lParam);
    }

    private static void ApplyDarkWindowFrame(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
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

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int attributeValue, int attributeSize);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint SetWindowsHookEx(int idHook, HookProc lpfn, nint hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(nint hhk);

    [DllImport("user32.dll")]
    private static extern nint CallNextHookEx(nint hhk, int nCode, nint wParam, nint lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
    private static extern nint GetModuleHandle(string? lpModuleName);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate nint HookProc(int nCode, nint wParam, nint lParam);
}

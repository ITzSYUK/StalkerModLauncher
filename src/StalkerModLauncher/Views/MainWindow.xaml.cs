using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using StalkerModLauncher.Models;
using StalkerModLauncher.Services;
using StalkerModLauncher.ViewModels;
using WpfDragEventArgs = System.Windows.DragEventArgs;
using WpfMouseEventArgs = System.Windows.Input.MouseEventArgs;
using WpfPoint = System.Windows.Point;

namespace StalkerModLauncher.Views;

public partial class MainWindow : Window
{
    private const int WhKeyboardLl = 13;
    private const int WmKeydown = 0x0100;
    private const int VkF2 = 0x71;
    private const int VkShift = 0x10;

    private WpfPoint _dragStartPoint;
    private NotesWindow? _notesWindow;
    private string? _notesProfileId;
    private nint _keyboardHook;
    private HookProc? _keyboardHookProc;
    private bool _isClosingAfterCleanup;

    public MainWindow()
    {
        InitializeComponent();
        var vm = new MainViewModel();
        vm.PropertyChanged += ViewModel_PropertyChanged;
        DataContext = vm;
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.IsLogVisible))
        {
            var row = RootGrid.RowDefinitions[2];
            var vm = (MainViewModel)DataContext;
            BindingOperations.SetBinding(row, RowDefinition.HeightProperty,
                new Binding(nameof(MainViewModel.LogRowHeight)) { Source = vm });
        }
    }

    private MainViewModel? ViewModel => DataContext as MainViewModel;

    private void Window_OnSourceInitialized(object? sender, EventArgs e)
    {
        ApplyDarkWindowFrame();
        InstallKeyboardHook();
    }

    private void InstallKeyboardHook()
    {
        _keyboardHookProc = LowLevelKeyboardProc;
        _keyboardHook = SetWindowsHookEx(WhKeyboardLl, _keyboardHookProc, GetModuleHandle(null), 0);
    }

    private nint LowLevelKeyboardProc(int nCode, nint wParam, nint lParam)
    {
        if (nCode >= 0 && wParam == WmKeydown)
        {
            var vkCode = Marshal.ReadInt32(lParam);
            if (vkCode == VkF2 && IsShiftPressed())
            {
                ToggleNotesOverlay();
            }
        }

        return CallNextHookEx(nint.Zero, nCode, wParam, lParam);
    }

    private static bool IsShiftPressed()
    {
        return (GetAsyncKeyState(VkShift) & 0x8000) != 0;
    }

    private void ToggleNotesOverlay()
    {
        var profile = ViewModel?.SelectedProfile;
        if (profile is null)
        {
            return;
        }

        if (_notesWindow is not null)
        {
            if (_notesProfileId == profile.Id)
            {
                _notesWindow.Close();
                return;
            }

            _notesWindow.Close();
        }

        ShowNotesForProfile(profile,
            window =>
            {
                window.Topmost = true;
                window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            });
    }

    private void ShowNotesForProfile(ModProfile profile, Action<NotesWindow>? configureWindow = null)
    {
        var notesVm = new NotesViewModel(profile, new AppPaths(), new DialogService());
        _notesWindow = new NotesWindow(notesVm);
        configureWindow?.Invoke(_notesWindow);
        _notesProfileId = profile.Id;
        _notesWindow.Closed += (_, _) =>
        {
            _notesWindow = null;
            _notesProfileId = null;
        };
        _notesWindow.Show();
        ForceForegroundWindow();
    }

    private void ForceForegroundWindow()
    {
        if (_notesWindow is null)
        {
            return;
        }

        var notesHandle = new WindowInteropHelper(_notesWindow).Handle;
        if (notesHandle == nint.Zero)
        {
            return;
        }

        var foregroundHandle = GetForegroundWindow();
        if (foregroundHandle == notesHandle)
        {
            return;
        }

        _ = ClipCursor(nint.Zero);

        if (foregroundHandle != nint.Zero)
        {
            var notesThread = GetWindowThreadProcessId(notesHandle, out _);
            var gameThread = GetWindowThreadProcessId(foregroundHandle, out _);
            _ = AttachThreadInput(notesThread, gameThread, true);
            _ = SetForegroundWindow(notesHandle);
            _ = AttachThreadInput(notesThread, gameThread, false);
        }
        else
        {
            _ = SetForegroundWindow(notesHandle);
        }

        _notesWindow.Activate();
        _notesWindow.Focus();
    }

    private void EditProfileButton_OnClick(object sender, RoutedEventArgs e)
    {
        var settingsVm = ViewModel?.CreateProfileSettingsViewModel();
        if (settingsVm is null)
        {
            return;
        }

        var window = new ProfileSettingsWindow(settingsVm)
        {
            Owner = this
        };

        window.ShowDialog();
    }

    private void NotesButton_OnClick(object sender, RoutedEventArgs e)
    {
        var profile = ViewModel?.SelectedProfile;
        if (profile is null)
        {
            return;
        }

        if (_notesWindow is not null)
        {
            if (_notesProfileId == profile.Id)
            {
                _notesWindow.Topmost = false;
                _notesWindow.ShowActivated = true;
                _notesWindow.Activate();
                return;
            }

            _notesWindow.Close();
        }

        ShowNotesForProfile(profile, window => window.Owner = this);
    }

    private void ScreenshotsButton_OnClick(object sender, RoutedEventArgs e)
    {
        var profile = ViewModel?.SelectedProfile;
        if (profile is null)
        {
            return;
        }

        var vm = new ScreenshotsViewModel(profile, ViewModel!.GameInstallPath);
        var window = new ScreenshotsWindow(vm)
        {
            Owner = this
        };

        window.ShowDialog();
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

    private ListViewItem? _dropTargetItem;

    private void ModsList_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragStartPoint = e.GetPosition(null);
    }

    private void ModsList_OnPreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject source)
        {
            var item = FindAncestor<ListViewItem>(source);
            if (item is not null && item.DataContext is ModEntry mod)
            {
                if (!ModsList.SelectedItems.Contains(mod))
                {
                    ModsList.SelectedItem = mod;
                }

                var contextMenu = new ContextMenu();
                var removeItem = new MenuItem { Header = "Убрать" };
                removeItem.Click += (_, _) =>
                {
                    var selected = ModsList.SelectedItems.Cast<ModEntry>().ToList();
                    ViewModel?.RemoveMods(selected);
                };
                contextMenu.Items.Add(removeItem);
                contextMenu.PlacementTarget = item;
                contextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint;
                contextMenu.IsOpen = true;

                e.Handled = true;
            }
        }
    }

    private void RemoveModsButton_Click(object sender, RoutedEventArgs e)
    {
        var selected = ModsList.SelectedItems.Cast<ModEntry>().ToList();
        ViewModel?.RemoveMods(selected);
    }

    private void ModsList_OnMouseMove(object sender, WpfMouseEventArgs e)
    {
        var currentPosition = e.GetPosition(null);
        if (e.LeftButton != MouseButtonState.Pressed ||
            Math.Abs(currentPosition.X - _dragStartPoint.X) < SystemParameters.MinimumHorizontalDragDistance ||
            Math.Abs(currentPosition.Y - _dragStartPoint.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        if (ModsList.SelectedItem is ModEntry mod)
        {
            DragDrop.DoDragDrop(ModsList, mod, DragDropEffects.Move);
        }
    }

    private void ModsList_OnDragOver(object sender, WpfDragEventArgs e)
    {
        if (!e.Data.GetDataPresent(typeof(ModEntry)))
        {
            e.Effects = DragDropEffects.None;
            return;
        }

        e.Effects = DragDropEffects.Move;
        e.Handled = true;

        if (e.OriginalSource is DependencyObject source)
        {
            var target = FindAncestor<ListViewItem>(source);
            if (target != _dropTargetItem)
            {
                ClearDropHighlight();

                _dropTargetItem = target;
                if (_dropTargetItem?.DataContext is ModEntry mod && _dropTargetItem.IsEnabled)
                {
                    var chrome = FindVisualChild<Border>(_dropTargetItem, "RowChrome");
                    if (chrome is not null)
                    {
                        chrome.BorderBrush = new SolidColorBrush(Color.FromRgb(0xCC, 0xCC, 0x00));
                        chrome.BorderThickness = new Thickness(2);
                    }
                }
            }
        }
    }

    private void ModsList_OnDragLeave(object sender, WpfDragEventArgs e)
    {
        ClearDropHighlight();
    }

    private void ModsList_OnDrop(object sender, WpfDragEventArgs e)
    {
        ClearDropHighlight();

        if (ViewModel is null)
        {
            return;
        }

        if (e.Data.GetDataPresent(typeof(ModEntry)))
        {
            var source = (ModEntry)e.Data.GetData(typeof(ModEntry))!;
            if (e.OriginalSource is not DependencyObject dependencyObject)
            {
                return;
            }

            var target = FindAncestor<ListViewItem>(dependencyObject) is { DataContext: ModEntry mod }
                ? mod
                : null;

            if (target is not null)
            {
                ViewModel.MoveMod(source, target);
            }
            else if (source != ViewModel.SelectedProfile?.Mods.LastOrDefault())
            {
                ViewModel.MoveModToEnd(source);
            }

            return;
        }

        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var paths = (string[])e.Data.GetData(DataFormats.FileDrop)!;
            ViewModel.AddDroppedMods(paths);
        }
    }

    private void ClearDropHighlight()
    {
        if (_dropTargetItem is not null)
        {
            var chrome = FindVisualChild<Border>(_dropTargetItem, "RowChrome");
            if (chrome is not null)
            {
                chrome.BorderBrush = new SolidColorBrush(Colors.Transparent);
                chrome.BorderThickness = new Thickness(1);
            }

            _dropTargetItem = null;
        }
    }

    private static T? FindVisualChild<T>(DependencyObject parent, string childName)
        where T : FrameworkElement
    {
        var count = VisualTreeHelper.GetChildrenCount(parent);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T typed && typed.Name == childName)
            {
                return typed;
            }

            var found = FindVisualChild<T>(child, childName);
            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }

    private void ModsList_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (sender is not ListView lv || lv.View is not GridView gv || gv.Columns.Count < 4)
        {
            return;
        }

        var scrollbarWidth = SystemParameters.VerticalScrollBarWidth;
        var fixedWidth = gv.Columns[0].Width + gv.Columns[1].Width + gv.Columns[2].Width + scrollbarWidth;
        var available = lv.ActualWidth - fixedWidth;
        if (available > 80)
        {
            gv.Columns[3].Width = available;
        }
    }

    private static T? FindAncestor<T>(DependencyObject current)
        where T : DependencyObject
    {
        while (current is not null)
        {
            if (current is T typed)
            {
                return typed;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return null;
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

    [DllImport("user32.dll")]
    private static extern bool ClipCursor(nint lpRect);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(nint hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(nint hWnd);

    [DllImport("user32.dll")]
    private static extern nint GetForegroundWindow();

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate nint HookProc(int nCode, nint wParam, nint lParam);

    private async void Title_MouseDown(object sender, MouseButtonEventArgs e)
    {
        var aboutWindow = new AboutWindow();
        if (ViewModel is not null)
        {
            var paths = new AppPaths();
            var settingsStore = new SettingsStore(paths);
            var settings = await settingsStore.LoadAsync();
            aboutWindow.DontShowAgain = settings.DontShowAboutOnStartup;
            aboutWindow.ShowDialog();
            if (aboutWindow.DontShowAgain != settings.DontShowAboutOnStartup)
            {
                _ = ViewModel.SaveAboutPreferenceAsync(aboutWindow.DontShowAgain);
            }
        }
        else
        {
            aboutWindow.ShowDialog();
        }
    }

    private async void Window_OnClosing(object? sender, CancelEventArgs e)
    {
        if (_isClosingAfterCleanup)
        {
            return;
        }

        e.Cancel = true;
        if (ViewModel is not null)
        {
            await ViewModel.CleanupAsync();
        }

        _isClosingAfterCleanup = true;
        Close();
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        if (_keyboardHook != nint.Zero)
        {
            _ = UnhookWindowsHookEx(_keyboardHook);
            _keyboardHook = nint.Zero;
        }
    }
}

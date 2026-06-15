using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using StalkerModLauncher.Models;
using StalkerModLauncher.ViewModels;
using StalkerModLauncher.Views;

namespace StalkerModLauncher.Services;

public sealed class WindowNavigationService
{
    private readonly AppPaths _paths;
    private readonly DialogService _dialogService;
    private readonly SettingsStore _settingsStore;
    private readonly ProfileHealthService _profileHealthService;
    private readonly WorkspaceManagementService _workspaceManagementService;
    private readonly ScreenshotScannerService _screenshotScannerService;
    private readonly IScreenshotClipboardService _screenshotClipboardService;
    private NotesWindow? _notesWindow;
    private string? _notesProfileId;

    public WindowNavigationService(
        AppPaths paths,
        DialogService dialogService,
        SettingsStore settingsStore,
        ProfileHealthService profileHealthService,
        WorkspaceManagementService workspaceManagementService,
        ScreenshotScannerService screenshotScannerService,
        IScreenshotClipboardService screenshotClipboardService)
    {
        _paths = paths;
        _dialogService = dialogService;
        _settingsStore = settingsStore;
        _profileHealthService = profileHealthService;
        _workspaceManagementService = workspaceManagementService;
        _screenshotScannerService = screenshotScannerService;
        _screenshotClipboardService = screenshotClipboardService;
    }

    public void ShowProfileCreation(Window owner, MainViewModel mainViewModel)
    {
        var wizard = new ProfileCreationWindow(new ProfileCreationViewModel(_dialogService)) { Owner = owner };
        if (wizard.ShowDialog() == true && wizard.CreatedProfile is not null)
        {
            mainViewModel.AddCreatedProfile(wizard.CreatedProfile);
        }
    }

    public void ShowProfileSettings(Window owner, ProfileSettingsViewModel viewModel)
    {
        new ProfileSettingsWindow(viewModel) { Owner = owner }.ShowDialog();
    }

    public void ShowScreenshots(Window owner, ModProfile profile)
    {
        var viewModel = new ScreenshotsViewModel(profile, _screenshotScannerService, _screenshotClipboardService);
        new ScreenshotsWindow(viewModel) { Owner = owner }.ShowDialog();
    }

    public void ShowProfileHealth(Window owner, ModProfile profile)
    {
        var viewModel = new ProfileHealthViewModel(
            profile,
            _profileHealthService,
            _dialogService,
            _workspaceManagementService);
        new ProfileHealthWindow(viewModel) { Owner = owner }.ShowDialog();
    }

    public void ShowNotes(Window owner, ModProfile profile)
    {
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

        ShowNotesForProfile(profile, window => window.Owner = owner);
    }

    public void ToggleNotesOverlay(ModProfile profile)
    {
        if (_notesWindow is not null)
        {
            if (_notesProfileId == profile.Id)
            {
                _notesWindow.Close();
                return;
            }

            _notesWindow.Close();
        }

        ShowNotesForProfile(
            profile,
            window =>
            {
                window.Topmost = true;
                window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            });
    }

    public async Task ShowAboutAsync(Window? owner = null, bool onlyIfNeeded = false)
    {
        var settings = await _settingsStore.LoadAsync();
        if (onlyIfNeeded && settings.DontShowAboutOnStartup)
        {
            return;
        }

        var aboutWindow = new AboutWindow
        {
            DontShowAgain = settings.DontShowAboutOnStartup,
            Owner = owner
        };
        aboutWindow.ShowDialog();

        if (aboutWindow.DontShowAgain != settings.DontShowAboutOnStartup)
        {
            await _settingsStore.UpdateAsync(current =>
            {
                current.DontShowAboutOnStartup = aboutWindow.DontShowAgain;
                return current;
            });
        }
    }

    private void ShowNotesForProfile(ModProfile profile, Action<NotesWindow>? configureWindow)
    {
        var notesViewModel = new NotesViewModel(profile, _paths, _dialogService);
        _notesWindow = new NotesWindow(notesViewModel);
        configureWindow?.Invoke(_notesWindow);
        _notesProfileId = profile.Id;
        _notesWindow.Closed += (_, _) =>
        {
            _notesWindow = null;
            _notesProfileId = null;
        };
        _notesWindow.Show();
        ForceNotesWindowToForeground();
    }

    private void ForceNotesWindowToForeground()
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
            var foregroundThread = GetWindowThreadProcessId(foregroundHandle, out _);
            _ = AttachThreadInput(notesThread, foregroundThread, true);
            _ = SetForegroundWindow(notesHandle);
            _ = AttachThreadInput(notesThread, foregroundThread, false);
        }
        else
        {
            _ = SetForegroundWindow(notesHandle);
        }

        _notesWindow.Activate();
        _notesWindow.Focus();
    }

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
}

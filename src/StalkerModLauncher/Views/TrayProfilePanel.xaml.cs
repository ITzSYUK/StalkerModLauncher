using System.Windows;
using System.Windows.Media.Animation;
using StalkerModLauncher.Infrastructure;
using StalkerModLauncher.Models;
using StalkerModLauncher.Services;
using StalkerModLauncher.ViewModels;
using Forms = System.Windows.Forms;

namespace StalkerModLauncher.Views;

public partial class TrayProfilePanel : Window
{
    private static readonly Duration ShowAnimationDuration = TimeSpan.FromMilliseconds(110);
    private readonly Action _openLauncher;
    private DateTime _lastHiddenAtUtc = DateTime.MinValue;

    public TrayProfilePanel()
        : this(() => { })
    {
    }

    private TrayProfilePanel(Action openLauncher)
    {
        _openLauncher = openLauncher;
        InitializeComponent();
        ProfileSidebar.PrimaryProfileActionCommand = new RelayCommand(
            LaunchProfile,
            CanLaunchProfile);
    }

    public TrayProfilePanel(MainViewModel viewModel, Action openLauncher)
        : this(openLauncher)
    {
        DataContext = viewModel;
    }

    public bool WasRecentlyHidden => DateTime.UtcNow - _lastHiddenAtUtc < TimeSpan.FromMilliseconds(250);

    public bool IsPanelOpen => IsVisible;

    public void ShowNearTray()
    {
        if (IsVisible)
        {
            return;
        }

        if (DataContext is MainViewModel viewModel)
        {
            ProfileSidebar.Height = Math.Clamp(108 + viewModel.Profiles.Count * 40, 190, 510);
        }

        var screen = Forms.Screen.FromPoint(Forms.Cursor.Position);
        var workArea = new Rect(
            screen.WorkingArea.Left,
            screen.WorkingArea.Top,
            screen.WorkingArea.Width,
            screen.WorkingArea.Height);
        var screenBounds = new Rect(
            screen.Bounds.Left,
            screen.Bounds.Top,
            screen.Bounds.Width,
            screen.Bounds.Height);
        var position = TrayPanelPlacement.Calculate(
            workArea,
            screenBounds,
            new Size(
                PanelRoot.Width + PanelRoot.Margin.Left + PanelRoot.Margin.Right,
                ProfileSidebar.Height + PanelRoot.Margin.Top + PanelRoot.Margin.Bottom));
        Left = position.X;
        Top = position.Y;
        Opacity = 0;
        Show();
        Activate();
        BeginAnimation(
            OpacityProperty,
            new DoubleAnimation(0, 1, ShowAnimationDuration),
            HandoffBehavior.SnapshotAndReplace);
    }

    public void HidePanel()
    {
        if (IsVisible)
        {
            _lastHiddenAtUtc = DateTime.UtcNow;
            BeginAnimation(OpacityProperty, null);
            Opacity = 1;
            Hide();
        }
    }

    public void ClosePanel()
    {
        BeginAnimation(OpacityProperty, null);
        Close();
    }

    private bool CanLaunchProfile(object? parameter) =>
        parameter is ModProfile profile &&
        DataContext is MainViewModel viewModel &&
        viewModel.CanLaunchProfile(profile);

    private async void LaunchProfile(object? parameter)
    {
        if (parameter is not ModProfile profile || DataContext is not MainViewModel viewModel)
        {
            return;
        }

        HidePanel();
        await viewModel.LaunchProfileAsync(profile);
    }

    private void ProfileSidebar_OnOpenLauncherRequested(object sender, RoutedEventArgs e)
    {
        HidePanel();
        _openLauncher();
    }

    private void Window_OnDeactivated(object? sender, EventArgs e) => HidePanel();

    private void Window_OnClosed(object? sender, EventArgs e) => _lastHiddenAtUtc = DateTime.UtcNow;
}

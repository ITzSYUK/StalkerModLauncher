using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using StalkerModLauncher.Infrastructure;
using StalkerModLauncher.Models;
using StalkerModLauncher.Services;
using StalkerModLauncher.ViewModels;
using Forms = System.Windows.Forms;

namespace StalkerModLauncher.Views;

public partial class TrayProfilePanel : Window
{
    private static readonly Duration ShowAnimationDuration = TimeSpan.FromMilliseconds(160);
    private static readonly Duration HideAnimationDuration = TimeSpan.FromMilliseconds(120);
    private readonly Action _openLauncher;
    private readonly RelayCommand _launchProfileCommand;
    private readonly HashSet<ModProfile> _trackedProfiles = [];
    private MainViewModel? _viewModel;
    private DateTime _lastHiddenAtUtc = DateTime.MinValue;
    private bool _isHiding;

    public TrayProfilePanel()
        : this(() => { })
    {
    }

    private TrayProfilePanel(Action openLauncher)
    {
        _openLauncher = openLauncher;
        InitializeComponent();
        _launchProfileCommand = new RelayCommand(
            LaunchProfile,
            CanLaunchProfile);
        ProfileSidebar.PrimaryProfileActionCommand = _launchProfileCommand;
    }

    public TrayProfilePanel(MainViewModel viewModel, Action openLauncher)
        : this(openLauncher)
    {
        DataContext = viewModel;
        _viewModel = viewModel;
        _viewModel.Profiles.CollectionChanged += ProfilesOnCollectionChanged;
        SynchronizeProfileSubscriptions();
        _launchProfileCommand.RaiseCanExecuteChanged();
    }

    public bool WasRecentlyHidden => DateTime.UtcNow - _lastHiddenAtUtc < TimeSpan.FromMilliseconds(250);

    public bool IsPanelOpen => IsVisible && !_isHiding;

    public void ShowNearTray()
    {
        if (IsVisible)
        {
            if (_isHiding)
            {
                _isHiding = false;
                BeginAnimation(OpacityProperty, null);
                PanelTranslate.BeginAnimation(TranslateTransform.YProperty, null);
                Opacity = 1;
                PanelTranslate.Y = 0;
            }

            return;
        }

        if (DataContext is MainViewModel viewModel)
        {
            viewModel.RefreshProfileLaunchReadiness(forceRefresh: true);
            _launchProfileCommand.RaiseCanExecuteChanged();
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
        PanelTranslate.Y = 10;
        Show();
        Activate();
        BeginAnimation(
            OpacityProperty,
            CreateAnimation(0, 1, ShowAnimationDuration, EasingMode.EaseOut),
            HandoffBehavior.SnapshotAndReplace);
        PanelTranslate.BeginAnimation(
            TranslateTransform.YProperty,
            CreateAnimation(10, 0, ShowAnimationDuration, EasingMode.EaseOut),
            HandoffBehavior.SnapshotAndReplace);
    }

    public void HidePanel()
    {
        if (!IsVisible || _isHiding)
        {
            return;
        }

        _lastHiddenAtUtc = DateTime.UtcNow;
        _isHiding = true;
        var opacityAnimation = CreateAnimation(Opacity, 0, HideAnimationDuration, EasingMode.EaseIn);
        opacityAnimation.Completed += (_, _) =>
        {
            if (!_isHiding)
            {
                return;
            }

            _isHiding = false;
            BeginAnimation(OpacityProperty, null);
            PanelTranslate.BeginAnimation(TranslateTransform.YProperty, null);
            Opacity = 1;
            PanelTranslate.Y = 10;
            Hide();
        };

        BeginAnimation(OpacityProperty, opacityAnimation, HandoffBehavior.SnapshotAndReplace);
        PanelTranslate.BeginAnimation(
            TranslateTransform.YProperty,
            CreateAnimation(PanelTranslate.Y, 8, HideAnimationDuration, EasingMode.EaseIn),
            HandoffBehavior.SnapshotAndReplace);
    }

    public void ClosePanel()
    {
        _isHiding = false;
        BeginAnimation(OpacityProperty, null);
        PanelTranslate.BeginAnimation(TranslateTransform.YProperty, null);
        Close();
    }

    private static DoubleAnimation CreateAnimation(
        double from,
        double to,
        Duration duration,
        EasingMode easingMode) =>
        new(from, to, duration)
        {
            EasingFunction = new CubicEase { EasingMode = easingMode }
        };

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

    private void ProfilesOnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        SynchronizeProfileSubscriptions();
        _launchProfileCommand.RaiseCanExecuteChanged();
    }

    private void SynchronizeProfileSubscriptions()
    {
        if (_viewModel is null)
        {
            return;
        }

        foreach (var profile in _trackedProfiles.Where(profile => !_viewModel.Profiles.Contains(profile)).ToArray())
        {
            profile.PropertyChanged -= ProfileOnPropertyChanged;
            _trackedProfiles.Remove(profile);
        }

        foreach (var profile in _viewModel.Profiles)
        {
            if (_trackedProfiles.Add(profile))
            {
                profile.PropertyChanged += ProfileOnPropertyChanged;
            }
        }
    }

    private void ProfileOnPropertyChanged(object? sender, PropertyChangedEventArgs e) =>
        _launchProfileCommand.RaiseCanExecuteChanged();

    private void Window_OnClosed(object? sender, EventArgs e)
    {
        _lastHiddenAtUtc = DateTime.UtcNow;
        if (_viewModel is not null)
        {
            _viewModel.Profiles.CollectionChanged -= ProfilesOnCollectionChanged;
        }

        foreach (var profile in _trackedProfiles)
        {
            profile.PropertyChanged -= ProfileOnPropertyChanged;
        }

        _trackedProfiles.Clear();
    }
}

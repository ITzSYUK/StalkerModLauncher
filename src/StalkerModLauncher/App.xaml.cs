using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using StalkerModLauncher.Infrastructure;
using StalkerModLauncher.Services;

namespace StalkerModLauncher;

public partial class App : Application
{
    private readonly AppServices _services = new();
    private readonly SingleInstanceGuard _singleInstance = new("StalkerModLauncher");
    private readonly UiSoundService _uiSoundService = new();

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        EventManager.RegisterClassHandler(typeof(ButtonBase), ButtonBase.ClickEvent, new RoutedEventHandler(ButtonBase_OnClick));
        EventManager.RegisterClassHandler(typeof(ListBox), Selector.SelectionChangedEvent, new SelectionChangedEventHandler(ListBox_OnSelectionChanged));

        if (!_singleInstance.IsPrimaryInstance)
        {
            MessageBox.Show(
                "Лаунчер уже запущен. Используйте открытое окно программы.",
                "S.T.A.L.K.E.R. Mod Launcher",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            Shutdown();
            return;
        }

        _uiSoundService.Initialize();

        BitmapImage? bitmap = null;
        try
        {
            var asm = typeof(App).Assembly;
            var name = asm.GetManifestResourceNames()
                .FirstOrDefault(n => n.EndsWith("ModLauncherLogo.png", StringComparison.OrdinalIgnoreCase));
            if (name is not null)
            {
                using var stream = asm.GetManifestResourceStream(name);
                if (stream is not null)
                {
                    bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.StreamSource = stream;
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    bitmap.Freeze();
                }
            }
        }
        catch
        {
        }

        if (bitmap is null)
        {
            var main = CreateMainWindow();
            main.Show();
            _ = ShowAboutIfNeededAsync(main);
            return;
        }

        var width = Math.Min(bitmap.PixelWidth, 600);
        var height = Math.Min(bitmap.PixelHeight, 400);

        var splash = new Window
        {
            WindowStyle = WindowStyle.None,
            ResizeMode = ResizeMode.NoResize,
            AllowsTransparency = true,
            Background = null,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            Width = width,
            Height = height,
            Opacity = 0,
            Topmost = true,
            Content = new Image
            {
                Source = bitmap,
                Stretch = Stretch.Uniform
            },
            ShowInTaskbar = false
        };

        splash.Show();

        var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(400));
        fadeIn.Completed += (_, _) =>
        {
            var timer = new DoubleAnimation(1, 1, TimeSpan.FromMilliseconds(1500));
            timer.Completed += async (_, _) =>
            {
                var main = CreateMainWindow();
                main.Show();
                splash.Close();
                await ShowAboutIfNeededAsync(main);
            };
            splash.BeginAnimation(UIElement.OpacityProperty, timer);
        };
        splash.BeginAnimation(UIElement.OpacityProperty, fadeIn);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _uiSoundService.Dispose();
        _singleInstance.Dispose();
        base.OnExit(e);
    }

    private void ButtonBase_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not ButtonBase button || button is RepeatButton)
        {
            return;
        }

        if (UiSound.GetKind(button) == UiSoundKind.ProfileActionsToggle && button is ToggleButton toggle)
        {
            _uiSoundService.Play(toggle.IsChecked == true
                ? UiSoundEffect.ProfileActionsOpened
                : UiSoundEffect.ProfileActionsClosed);
            return;
        }

        _uiSoundService.Play(UiSoundEffect.ButtonPress);
    }

    private void ListBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not ListBox { Name: "ProfilesList", IsLoaded: true } profileList ||
            e.AddedItems.Count == 0 ||
            (!profileList.IsMouseOver && !profileList.IsKeyboardFocusWithin))
        {
            return;
        }

        _uiSoundService.Play(UiSoundEffect.ButtonPress);
    }

    private Views.MainWindow CreateMainWindow()
    {
        return new Views.MainWindow(
            _services.CreateMainViewModel(),
            _services.WindowNavigationService,
            _services.WindowSystemIntegrationService);
    }

    private async Task ShowAboutIfNeededAsync(Window? owner = null)
    {
        await _services.WindowNavigationService.ShowAboutAsync(owner, onlyIfNeeded: true);
    }
}

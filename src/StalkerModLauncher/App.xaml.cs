using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using StalkerModLauncher.Services;

namespace StalkerModLauncher;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

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
            var main = new Views.MainWindow();
            main.Show();
            _ = ShowAboutIfNeededAsync();
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
                var main = new Views.MainWindow();
                main.Show();
                splash.Close();
                await ShowAboutIfNeededAsync();
            };
            splash.BeginAnimation(UIElement.OpacityProperty, timer);
        };
        splash.BeginAnimation(UIElement.OpacityProperty, fadeIn);
    }

    private static async Task ShowAboutIfNeededAsync()
    {
        var paths = new AppPaths();
        var settingsStore = new SettingsStore(paths);
        var settings = await settingsStore.LoadAsync();

        if (!settings.DontShowAboutOnStartup)
        {
            var about = new Views.AboutWindow
            {
                DontShowAgain = false
            };
            about.ShowDialog();

            if (about.DontShowAgain)
            {
                settings.DontShowAboutOnStartup = true;
                await settingsStore.SaveAsync(settings);
            }
        }
    }
}
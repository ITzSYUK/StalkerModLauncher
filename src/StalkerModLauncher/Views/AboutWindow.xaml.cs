using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using StalkerModLauncher.Services;

namespace StalkerModLauncher.Views;

public partial class AboutWindow : Window
{
    private readonly LauncherUpdateService _launcherUpdateService;
    private readonly DialogService _dialogService;
    private readonly WindowSystemIntegrationService _windowSystemIntegrationService;

    public static readonly DependencyProperty DontShowAgainProperty =
        DependencyProperty.Register(nameof(DontShowAgain), typeof(bool), typeof(AboutWindow), new PropertyMetadata(false));

    public bool DontShowAgain
    {
        get => (bool)GetValue(DontShowAgainProperty);
        set => SetValue(DontShowAgainProperty, value);
    }

    public AboutWindow(
        LauncherUpdateService launcherUpdateService,
        DialogService dialogService,
        WindowSystemIntegrationService windowSystemIntegrationService)
    {
        InitializeComponent();
        _launcherUpdateService = launcherUpdateService;
        _dialogService = dialogService;
        _windowSystemIntegrationService = windowSystemIntegrationService;
        VersionTextBlock.Text = GetVersionText();
    }

    private static string GetVersionText()
    {
        var assembly = typeof(AboutWindow).Assembly;
        var informationalVersion = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;
        var version = informationalVersion?.Split('+')[0] ?? assembly.GetName().Version?.ToString(3) ?? "неизвестна";
        return $"Версия {version}";
    }

    private void AboutWindow_OnSourceInitialized(object? sender, EventArgs e)
    {
        ApplyDarkWindowFrame();
    }

    private void CloseButton_OnClick(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private async void CheckUpdatesButton_OnClick(object sender, RoutedEventArgs e)
    {
        CheckUpdatesButton.IsEnabled = false;
        CheckUpdatesButton.Content = "Проверяем...";

        try
        {
            var result = await _launcherUpdateService.CheckAsync();
            if (!result.IsUpdateAvailable)
            {
                _dialogService.ShowInfo(
                    "Проверка обновлений",
                    $"Установлена актуальная версия лаунчера: {result.CurrentVersion}.");
                return;
            }

            var updateWindow = new UpdateAvailableWindow(
                result.CurrentVersion,
                result.LatestVersion,
                _windowSystemIntegrationService)
            {
                Owner = this
            };

            if (updateWindow.ShowDialog() == true)
            {
                _dialogService.OpenUrl(result.ReleaseUrl);
            }
        }
        catch (TaskCanceledException)
        {
            _dialogService.ShowError(
                "Проверка обновлений",
                "GitHub не ответил вовремя. Проверьте подключение к интернету и повторите попытку.");
        }
        catch (HttpRequestException)
        {
            _dialogService.ShowError(
                "Проверка обновлений",
                "Не удалось получить информацию о последнем релизе с GitHub. Проверьте подключение к интернету и повторите попытку.");
        }
        catch (Exception ex)
        {
            _dialogService.ShowError(
                "Проверка обновлений",
                $"Не удалось проверить обновления: {ex.Message}");
        }
        finally
        {
            CheckUpdatesButton.Content = "Проверить обновления";
            CheckUpdatesButton.IsEnabled = true;
        }
    }

    private void ApplyDarkWindowFrame()
    {
        var handle = new WindowInteropHelper(this).Handle;
        if (handle == IntPtr.Zero)
            return;

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

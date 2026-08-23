using System.Net.Http;
using System.Reflection;
using System.Windows;
using System.Windows.Media;
using StalkerModLauncher.Services;

namespace StalkerModLauncher.Views;

public partial class AboutWindow : Window
{
    private readonly LauncherUpdateService _launcherUpdateService;
    private readonly Action? _openPdaInterface;
    private string? _releaseUrl;

    public static readonly DependencyProperty DontShowAgainProperty =
        DependencyProperty.Register(nameof(DontShowAgain), typeof(bool), typeof(AboutWindow), new PropertyMetadata(false));

    public bool DontShowAgain
    {
        get => (bool)GetValue(DontShowAgainProperty);
        set => SetValue(DontShowAgainProperty, value);
    }

    public AboutWindow(
        LauncherUpdateService launcherUpdateService,
        Action? openPdaInterface = null)
    {
        InitializeComponent();
        _launcherUpdateService = launcherUpdateService;
        _openPdaInterface = openPdaInterface;
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
        WindowSystemIntegrationService.Initialize(this);
    }

    private void PdaInterfaceButton_OnClick(object sender, RoutedEventArgs e)
    {
        Close();
        _openPdaInterface?.Invoke();
    }

    private async void CheckUpdatesButton_OnClick(object sender, RoutedEventArgs e)
    {
        CheckUpdatesButton.IsEnabled = false;
        CheckUpdatesButton.Content = "Проверяем...";
        _releaseUrl = null;
        OpenReleaseButton.Visibility = Visibility.Collapsed;
        ShowUpdateStatus("Проверяем GitHub...", "MutedTextBrush");

        try
        {
            var result = await _launcherUpdateService.CheckAsync();
            if (result.IsUpdateAvailable)
            {
                _releaseUrl = result.ReleaseUrl;
                OpenReleaseButton.Visibility = Visibility.Visible;
                ShowUpdateStatus(
                    $"Доступна версия {result.LatestVersion}. Установлена {result.CurrentVersion}.",
                    "AccentBrush");
            }
            else
            {
                ShowUpdateStatus(
                    $"Установлена актуальная версия лаунчера: {result.CurrentVersion}.",
                    "MutedTextBrush");
            }
        }
        catch (TaskCanceledException)
        {
            ShowUpdateStatus(
                "GitHub не ответил вовремя. Проверьте подключение к интернету и повторите попытку.",
                "DangerBrush");
        }
        catch (HttpRequestException)
        {
            ShowUpdateStatus(
                "Не удалось получить информацию о последнем релизе с GitHub. Проверьте подключение к интернету и повторите попытку.",
                "DangerBrush");
        }
        catch (Exception ex)
        {
            ShowUpdateStatus($"Не удалось проверить обновления: {ex.Message}", "DangerBrush");
        }
        finally
        {
            CheckUpdatesButton.Content = "Проверить обновления";
            CheckUpdatesButton.IsEnabled = true;
        }
    }

    private void OpenReleaseButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(_releaseUrl))
        {
            DialogService.OpenUrl(_releaseUrl);
        }
    }

    private void ShowUpdateStatus(string message, string brushResourceKey)
    {
        UpdateStatusText.Text = message;
        UpdateStatusText.Foreground = (Brush)FindResource(brushResourceKey);
        UpdateStatusPanel.Visibility = Visibility.Visible;
    }
}

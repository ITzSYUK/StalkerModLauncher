using System.Net.Http;
using System.Reflection;
using System.Windows;
using StalkerModLauncher.Services;

namespace StalkerModLauncher.Views;

public partial class AboutWindow : Window
{
    private readonly LauncherUpdateService _launcherUpdateService;
    private readonly DialogService _dialogService;
    private readonly Action? _openPdaInterface;

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
        Action? openPdaInterface = null)
    {
        InitializeComponent();
        _launcherUpdateService = launcherUpdateService;
        _dialogService = dialogService;
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

        try
        {
            var result = await _launcherUpdateService.CheckAsync();
            if (!result.IsUpdateAvailable)
            {
                DialogService.ShowInfo(
                    "Проверка обновлений",
                    $"Установлена актуальная версия лаунчера: {result.CurrentVersion}.");
                return;
            }

            var updateWindow = new UpdateAvailableWindow(
                result.CurrentVersion,
                result.LatestVersion)
            {
                Owner = this
            };

            if (updateWindow.ShowDialog() == true)
            {
                DialogService.OpenUrl(result.ReleaseUrl);
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

}

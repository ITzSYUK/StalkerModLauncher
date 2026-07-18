using System.Net.Http;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using StalkerModLauncher.Services;

namespace StalkerModLauncher.Views.Controls;

public partial class PdaAboutView : UserControl
{
    private readonly WindowNavigationService _navigation;
    private string? _releaseUrl;

    public PdaAboutView(WindowNavigationService navigation)
    {
        _navigation = navigation;
        InitializeComponent();
        VersionText.Text = $"Версия {GetVersion()}";
    }

    private async void CheckUpdatesButton_OnClick(object sender, RoutedEventArgs e)
    {
        CheckUpdatesButton.IsEnabled = false;
        UpdateStatusText.Text = "Проверяем GitHub...";
        OpenReleaseButton.Visibility = Visibility.Collapsed;
        try
        {
            var result = await _navigation.CheckForUpdatesAsync();
            if (result.IsUpdateAvailable)
            {
                _releaseUrl = result.ReleaseUrl;
                UpdateStatusText.Text = $"Доступна версия {result.LatestVersion}.";
                OpenReleaseButton.Visibility = Visibility.Visible;
            }
            else
            {
                UpdateStatusText.Text = $"Установлена актуальная версия {result.CurrentVersion}.";
            }
        }
        catch (TaskCanceledException)
        {
            UpdateStatusText.Text = "GitHub не ответил вовремя.";
        }
        catch (HttpRequestException)
        {
            UpdateStatusText.Text = "Не удалось подключиться к GitHub.";
        }
        catch (Exception ex)
        {
            UpdateStatusText.Text = $"Проверка не выполнена: {ex.Message}";
        }
        finally
        {
            CheckUpdatesButton.IsEnabled = true;
        }
    }

    private void OpenReleaseButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(_releaseUrl))
        {
            _navigation.OpenUrl(_releaseUrl);
        }
    }

    private static string GetVersion()
    {
        var assembly = typeof(PdaAboutView).Assembly;
        return assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion?.Split('+')[0]
               ?? assembly.GetName().Version?.ToString(3)
               ?? "неизвестна";
    }
}

using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Xml.Linq;
using StalkerModLauncher.Views;
using StalkerModLauncher.Views.Controls;
using Xunit;

namespace StalkerModLauncher.Tests;

public sealed class LauncherShellUiTests
{
    [Fact]
    public void LauncherSettingsRendersAtClassicAndPdaContentSize()
    {
        var settings = LoadProjectXaml("Views", "Controls", "LauncherSettingsView.xaml");
        var classicWindow = LoadProjectXaml("Views", "LauncherSettingsWindow.xaml");

        Assert.DoesNotContain("ПОВЕДЕНИЕ", settings.ToString());
        Assert.Contains("FontSize=\"12\"", settings.ToString());
        Assert.Contains("Width=\"20\" Height=\"20\"", settings.ToString());
        Assert.Contains("Уровень записи launcher.log", settings.ToString());
        Assert.Contains("Text=\"Настройки\"", settings.ToString());
        Assert.Contains("TextWrapping=\"NoWrap\"", settings.ToString());
        Assert.Contains("ОБНОВЛЕНИЕ", settings.ToString());
        Assert.Contains("Проверить обновления", settings.ToString());
        Assert.Contains("CheckForUpdatesCommand", settings.ToString());
        Assert.Contains("Открыть релиз", settings.ToString());
        Assert.Contains("OpenReleaseButton", settings.ToString());
        Assert.Contains("Скачать в Загрузки", settings.ToString());
        Assert.Contains("DownloadToDownloadsButton", settings.ToString());
        Assert.Contains("Открыть Загрузки", settings.ToString());
        Assert.Contains("Minimal version", settings.ToString());
        Assert.Contains("Standalone version", settings.ToString());
        Assert.Contains("Автоматически проверять обновления при запуске", settings.ToString());
        Assert.Contains("При запуске вместе с Windows открывать в трее", settings.ToString());
        Assert.Contains("StartMinimizedToTrayOnWindowsStartup", settings.ToString());
        Assert.Contains("Показывать значок в трее", settings.ToString());
        Assert.Contains("CanStartMinimizedToTray", settings.ToString());
        Assert.Contains("Показывать системное уведомление о доступном обновлении", settings.ToString());
        Assert.Contains("ShowUpdateNotifications", settings.ToString());
        Assert.Contains("Сбросить настройки", settings.ToString());
        Assert.Contains("ResetCommand", settings.ToString());
        Assert.Contains("LauncherSettingsPanelCornerRadius", settings.ToString());
        Assert.Contains("техническая диагностика запуска, Workspace и USVFS", settings.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Только ошибки —", settings.ToString());
        Assert.Equal("760", (string?)classicWindow.Root?.Attribute("Width"));

        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        var behaviorOptions = settings.Descendants(presentation + "CheckBox").ToList();
        var startWithWindowsIndex = behaviorOptions.FindIndex(element =>
            (string?)element.Attribute("Content") == "Запускать вместе с Windows");
        var showTrayIconIndex = behaviorOptions.FindIndex(element =>
            (string?)element.Attribute("Content") == "Показывать значок в трее");
        Assert.True(startWithWindowsIndex < showTrayIconIndex);
        var minimizeToTray = Assert.Single(behaviorOptions, element =>
            (string?)element.Attribute("Content") == "Сворачивать в трей вместо закрытия");
        Assert.Equal("26,0,0,0", (string?)minimizeToTray.Attribute("Margin"));
        Assert.Equal("{Binding CanUseTray}", (string?)minimizeToTray.Attribute("IsEnabled"));

        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var host = CreateThemeHost();
                var classicSettings = new LauncherSettingsView();
                host.Children.Add(classicSettings);
                host.Measure(new Size(600, 480));
                host.Arrange(new Rect(0, 0, 600, 480));
                host.UpdateLayout();

                var classicPanel = Assert.IsType<Border>(classicSettings.FindName("SettingsPanelBorder"));
                Assert.Equal(new CornerRadius(5), classicPanel.CornerRadius);

                var bitmap = new RenderTargetBitmap(600, 480, 96, 96, PixelFormats.Pbgra32);
                bitmap.Render(host);
                Assert.Equal(600, bitmap.PixelWidth);

                var pda = CreatePdaThemeHost();
                var pdaSettings = new LauncherSettingsView();
                pda.Children.Add(pdaSettings);
                pda.Measure(new Size(908, 521));
                pda.Arrange(new Rect(0, 0, 908, 521));
                pda.UpdateLayout();

                var pdaPanel = Assert.IsType<Border>(pdaSettings.FindName("SettingsPanelBorder"));
                Assert.Equal(new CornerRadius(0), pdaPanel.CornerRadius);

                var pdaBitmap = new RenderTargetBitmap(908, 521, 96, 96, PixelFormats.Pbgra32);
                pdaBitmap.Render(pda);
                Assert.Equal(908, pdaBitmap.PixelWidth);
            }
            catch (Exception ex)
            {
                failure = ex;
            }
            finally
            {
                System.Windows.Threading.Dispatcher.CurrentDispatcher.InvokeShutdown();
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        Assert.Null(failure);
    }

    [Fact]
    public void TrayPanelHidesBackendSelectionAndAnimatesPlayButton()
    {
        var trayDocument = LoadProjectXaml("Views", "TrayProfilePanel.xaml");
        var sidebarDocument = LoadProjectXaml("Views", "Controls", "ProfileSidebarView.xaml");
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

        var traySidebar = trayDocument.Descendants()
            .Single(element => element.Name.LocalName == "ProfileSidebarView");
        var trayStyle = sidebarDocument.Descendants(presentation + "Style")
            .Single(style => (string?)style.Attribute(xaml + "Key") == "TrayProfileListBoxItemStyle");

        Assert.Equal("True", (string?)traySidebar.Attribute("IsTrayMode"));
        Assert.Empty(trayDocument.Descendants(presentation + "ComboBox"));
        Assert.DoesNotContain("SecondaryProfileActionCommand", trayStyle.ToString());
        Assert.Contains("PrimaryProfileActionCommand", trayStyle.ToString());
        Assert.DoesNotContain("Workspace", trayStyle.ToString());
        Assert.DoesNotContain("USVFS", trayStyle.ToString());
        Assert.Contains("Запустить", trayStyle.ToString());
        Assert.DoesNotContain("Запустить профиль", trayStyle.ToString());
        Assert.Contains(
            sidebarDocument.Descendants(presentation + "Setter"),
            setter => (string?)setter.Attribute("Property") == "MaxHeight" &&
                      (string?)setter.Attribute("Value") == "34");
        Assert.DoesNotContain("IsSelected", trayStyle.ToString());
        var profileList = sidebarDocument.Descendants(presentation + "ListBox")
            .Single(element => (string?)element.Attribute(xaml + "Name") == "ProfilesList");
        Assert.Null(profileList.Attribute("SelectedItem"));
        Assert.Contains(
            sidebarDocument.Descendants(presentation + "DataTrigger")
                .Where(trigger => (string?)trigger.Attribute("Value") == "True")
                .SelectMany(trigger => trigger.Elements(presentation + "Setter")),
            setter => (string?)setter.Attribute("Property") == "SelectedItem" &&
                      (string?)setter.Attribute("Value") == "{x:Null}");
        Assert.Contains(
            trayStyle.Descendants(presentation + "Setter"),
            setter => (string?)setter.Attribute("Property") == "Focusable" &&
                      (string?)setter.Attribute("Value") == "False");
        Assert.Contains("Property=\"Foreground\" Value=\"{DynamicResource TextBrush}\"", sidebarDocument.ToString());
        Assert.Contains(
            trayStyle.Descendants(presentation + "DoubleAnimation"),
            animation => (string?)animation.Attribute("Storyboard.TargetName") == "ProfileActions" &&
                         (string?)animation.Attribute("To") == "1" &&
                         (string?)animation.Attribute("Duration") == "0:0:0.18");
        var profileActionsExit = trayStyle.Descendants(presentation + "DoubleAnimation")
            .Single(animation =>
                (string?)animation.Attribute("Storyboard.TargetName") == "ProfileActions" &&
                (string?)animation.Attribute("Storyboard.TargetProperty") ==
                "(UIElement.RenderTransform).(TranslateTransform.X)" &&
                (string?)animation.Attribute("To") == "18");
        var exitDuration = (string?)profileActionsExit.Attribute("Duration");
        Assert.NotNull(exitDuration);
        Assert.Equal(
            TimeSpan.FromMilliseconds(200),
            TimeSpan.Parse(exitDuration!, CultureInfo.InvariantCulture));
        Assert.Equal("BitmapCache", (string?)trayDocument.Descendants(presentation + "Grid")
            .Single(element => (string?)element.Attribute(xaml + "Name") == "PanelRoot")
            .Attribute("CacheMode"));
        Assert.Equal("Window", trayDocument.Root?.Name.LocalName);
        Assert.Equal("None", (string?)trayDocument.Root?.Attribute("WindowStyle"));
        Assert.Equal("False", (string?)trayDocument.Root?.Attribute("ShowInTaskbar"));
        Assert.Equal("Window_OnDeactivated", (string?)trayDocument.Root?.Attribute("Deactivated"));
        Assert.Contains("HasLaunchError", sidebarDocument.ToString());
        Assert.Contains("LaunchErrorSummary", sidebarDocument.ToString());
        Assert.Contains("Text=\"!\"", sidebarDocument.ToString());
        Assert.Contains(
            trayStyle.Descendants(presentation + "DataTrigger"),
            trigger => (string?)trigger.Attribute("Binding") == "{Binding HasLaunchError}" &&
                       (string?)trigger.Attribute("Value") == "True");
    }

    [Fact]
    public void AnomalyAutomaticRendererIsNamedLauncherInBothInterfaces()
    {
        var classic = LoadProjectXaml("Views", "ProfileSettingsWindow.xaml").ToString();
        var pda = LoadProjectXaml("Views", "Controls", "PdaProfileSettingsView.xaml").ToString();

        Assert.Contains("Content=\"Лаунчер\"", classic);
        Assert.Contains("Content=\"Лаунчер\"", pda);
        Assert.DoesNotContain("Content=\"Авто\"", classic);
        Assert.DoesNotContain("Content=\"Авто\"", pda);
    }

    [Fact]
    public void ProfileSettingsDoNotLabelBackendsAsStable()
    {
        var classic = LoadProjectXaml("Views", "ProfileSettingsWindow.xaml").ToString();
        var pda = LoadProjectXaml("Views", "Controls", "PdaProfileSettingsView.xaml").ToString();

        Assert.DoesNotContain("Workspace — стабильный", classic);
        Assert.DoesNotContain("USVFS — стабильный", classic);
        Assert.DoesNotContain("Workspace — стабильный", pda);
        Assert.DoesNotContain("USVFS — стабильный", pda);
        Assert.DoesNotContain("эксперимент", classic, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("эксперимент", pda, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TrayPopupCanBeReopenedImmediately()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            TrayProfilePanel? panel = null;
            try
            {
                panel = new TrayProfilePanel();
                panel.ShowNearTray();
                Assert.True(panel.IsPanelOpen);
                panel.HidePanel();
                Assert.False(panel.IsPanelOpen);
                Assert.True(panel.WasRecentlyHidden);
                for (var attempt = 0; attempt < 3; attempt++)
                {
                    panel.ShowNearTray();
                    Assert.True(panel.IsPanelOpen);
                    panel.HidePanel();
                    Assert.False(panel.IsPanelOpen);
                }
            }
            catch (Exception ex)
            {
                failure = ex;
            }
            finally
            {
                panel?.HidePanel();
                System.Windows.Threading.Dispatcher.CurrentDispatcher.InvokeShutdown();
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        Assert.Null(failure);
    }

    [Fact]
    public void TrayProfileRowsBlockSelectionInput()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var sidebar = new ProfileSidebarView { IsTrayMode = true };
                var profiles = Assert.IsType<ListBox>(sidebar.FindName("ProfilesList"));
                var profileItem = new ListBoxItem();
                var input = new MouseButtonEventArgs(
                    Mouse.PrimaryDevice,
                    Environment.TickCount,
                    MouseButton.Left)
                {
                    RoutedEvent = UIElement.PreviewMouseLeftButtonDownEvent,
                    Source = profileItem
                };

                profiles.RaiseEvent(input);

                Assert.True(input.Handled);
            }
            catch (Exception ex)
            {
                failure = ex;
            }
            finally
            {
                System.Windows.Threading.Dispatcher.CurrentDispatcher.InvokeShutdown();
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        Assert.Null(failure);
    }

    [Fact]
    public void BothInterfacesExposeLauncherSettingsButton()
    {
        var classic = LoadProjectXaml("Views", "Controls", "ProfileSidebarView.xaml");
        var pda = LoadProjectXaml("Views", "Controls", "PdaMainView.xaml");

        Assert.Contains("LauncherSettingsButton_OnClick", classic.ToString());
        Assert.Contains("LauncherSettingsButton_OnClick", pda.ToString());
        Assert.DoesNotContain("PdaSystemSettingsButtonStyle", pda.ToString());
        Assert.Equal(1, pda.Descendants()
            .Count(element => (string?)element.Attribute("Style") == "{StaticResource PdaCompactTopTabStyle}"));
        Assert.DoesNotContain("ToggleInterfaceCommand", pda.ToString());
        Assert.DoesNotContain("Вернуться к обычному интерфейсу", pda.ToString());
        Assert.Equal("3", (string?)pda.Descendants()
            .Single(element => (string?)element.Attribute("Click") == "PowerButton_OnClick")
            .Attribute("Grid.Column"));
        Assert.Equal(1, pda.Descendants()
            .Count(element => (string?)element.Attribute("Value") == "{StaticResource PdaPowerNormalBrush}"));
        Assert.DoesNotContain("ToolTip=\"Настройки лаунчера\"", classic.ToString());
        Assert.DoesNotContain("ToolTip=\"Настройки лаунчера\"", pda.ToString());
    }

    [Fact]
    public void ActivityLogsDoNotRepeatTheirPageTitles()
    {
        var classicLog = LoadProjectXaml("Views", "Controls", "ActivityLogView.xaml");
        var pdaLog = LoadProjectXaml("Views", "Controls", "PdaLogView.xaml");

        Assert.DoesNotContain("Text=\"Журнал\"", classicLog.ToString());
        Assert.DoesNotContain("ЖУРНАЛ ЛАУНЧЕРА", pdaLog.ToString());
    }

    [Fact]
    public void PdaCatalogModCardsHaveSquareCorners()
    {
        var catalog = LoadProjectXaml("Views", "Controls", "PdaModCatalogView.xaml");
        var card = catalog.Descendants()
            .Single(element =>
                element.Name.LocalName == "Border" &&
                (string?)element.Attribute("Padding") == "8" &&
                element.Attributes().Any(attribute =>
                    attribute.Name.LocalName == "Name" && attribute.Value == "Chrome"));

        Assert.Equal("0", (string?)card.Attribute("CornerRadius"));
    }

    [Fact]
    public void AboutPagesDoNotExposeUpdateOrInterfaceSwitchButtons()
    {
        var classicAbout = LoadProjectXaml("Views", "AboutWindow.xaml");
        var pdaAbout = LoadProjectXaml("Views", "Controls", "PdaAboutView.xaml");

        Assert.Contains("Text=\"CORDON\"", classicAbout.ToString());
        Assert.Contains("S.T.A.L.K.E.R. Mod Launcher", classicAbout.ToString());
        Assert.Contains("Text=\"CORDON\"", pdaAbout.ToString());
        Assert.Contains("S.T.A.L.K.E.R. Mod Launcher", pdaAbout.ToString());
        Assert.DoesNotContain("Проверить обновления", classicAbout.ToString());
        Assert.DoesNotContain("Интерфейс КПК", classicAbout.ToString());
        Assert.DoesNotContain("Проверить обновления", pdaAbout.ToString());
    }

    [Fact]
    public void LauncherBrandingUsesLargerClassicTextAndSingleLinePdaText()
    {
        var classicBrand = LoadProjectXaml("Views", "Controls", "LauncherBrand.xaml");
        var pdaMain = LoadProjectXaml("Views", "Controls", "PdaMainView.xaml");
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";

        var classicTitle = Assert.Single(classicBrand.Descendants(presentation + "TextBlock"), element =>
            (string?)element.Attribute("Text") == "CORDON");
        var classicSubtitle = Assert.Single(classicBrand.Descendants(presentation + "TextBlock"), element =>
            (string?)element.Attribute("Text") == "S.T.A.L.K.E.R. Mod Launcher");
        Assert.Equal("28", (string?)classicTitle.Attribute("FontSize"));
        Assert.Equal("12.5", (string?)classicSubtitle.Attribute("FontSize"));

        var pdaTitle = Assert.Single(pdaMain.Descendants(presentation + "TextBlock"), element =>
            element.Descendants(presentation + "Run").Any(run => (string?)run.Attribute("Text") == "CORDON"));
        Assert.Equal("NoWrap", (string?)pdaTitle.Attribute("TextWrapping"));
        Assert.Contains("S.T.A.L.K.E.R. Mod Launcher", pdaTitle.ToString());
        Assert.DoesNotContain(" — ", pdaTitle.ToString());
        Assert.Contains(pdaTitle.Descendants(presentation + "Run"), run =>
            (string?)run.Attribute("Text") == "\u00A0\u00A0");
    }

    private static Grid CreateThemeHost()
    {
        var host = new Grid { Width = 600, Height = 480 };
        host.Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri("/CORDON;component/Themes/Palette.xaml", UriKind.RelativeOrAbsolute)
        });
        host.Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri("/CORDON;component/Themes/SharedStyles.xaml", UriKind.RelativeOrAbsolute)
        });
        return host;
    }

    private static Grid CreatePdaThemeHost()
    {
        var host = new Grid { Width = 908, Height = 521 };
        host.Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri("/CORDON;component/Themes/PdaTheme.xaml", UriKind.RelativeOrAbsolute)
        });
        return host;
    }

    private static XDocument LoadProjectXaml(params string[] parts)
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            var projectRoot = Path.Combine(directory.FullName, "src", "StalkerModLauncher");
            if (File.Exists(Path.Combine(projectRoot, "StalkerModLauncher.csproj")))
            {
                return XDocument.Load(Path.Combine([projectRoot, .. parts]));
            }
        }

        throw new DirectoryNotFoundException("StalkerModLauncher project root was not found.");
    }
}

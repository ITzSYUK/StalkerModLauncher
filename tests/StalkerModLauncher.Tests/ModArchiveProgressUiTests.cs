using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Xml.Linq;
using Xunit;

namespace StalkerModLauncher.Tests;

public sealed class ModArchiveProgressUiTests
{
    [Fact]
    public void CompletionAndDestinationConflictUseSharedInlinePanels()
    {
        var projectRoot = FindProjectRoot();
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";

        var modPanel = XDocument.Load(Path.Combine(projectRoot, "Views", "Controls", "ModPanelView.xaml"));
        var completionTrigger = modPanel
            .Descendants(presentation + "DataTrigger")
            .Single(trigger => trigger
                .Attributes("Binding")
                .Any(binding => binding.Value.Contains(
                    "IsModArchiveInstallCompleted",
                    StringComparison.Ordinal)));
        Assert.Equal("True", (string?)completionTrigger.Attribute("Value"));
        Assert.Contains(
            modPanel.Descendants(presentation + "TextBlock"),
            textBlock => ((string?)textBlock.Attribute("Text"))?.Contains(
                "InstalledModArchiveName",
                StringComparison.Ordinal) == true);
        Assert.Contains(
            modPanel.Descendants(presentation + "MouseBinding"),
            binding => ((string?)binding.Attribute("Command"))?.Contains(
                "DismissModArchiveInstallCompletedCommand",
                StringComparison.Ordinal) == true);
        Assert.Contains(
            modPanel.Descendants(presentation + "TextBlock"),
            textBlock => ((string?)textBlock.Attribute("Text"))?.Contains(
                "ModArchiveInstallDestinationConflictText",
                StringComparison.Ordinal) == true);
        Assert.Contains(
            modPanel.Descendants(presentation + "Button"),
            button => ((string?)button.Attribute("Command"))?.Contains(
                "ContinueModArchiveInstallCommand",
                StringComparison.Ordinal) == true);
        Assert.False(File.Exists(Path.Combine(projectRoot, "Views", "ModArchiveInstalledWindow.xaml")));
    }

    [Fact]
    public void ProgressPanelRendersInClassicAndPdaThemes()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                Render(usePdaTheme: false);
                Render(usePdaTheme: true);
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

    private static void Render(bool usePdaTheme)
    {
        var view = new Grid
        {
            Width = 940,
            Height = 64
        };
        view.Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri("/CORDON;component/Themes/Palette.xaml", UriKind.RelativeOrAbsolute)
        });
        if (usePdaTheme)
        {
            view.Resources.MergedDictionaries.Add(new ResourceDictionary
            {
                Source = new Uri("/CORDON;component/Themes/PdaTheme.xaml", UriKind.RelativeOrAbsolute)
            });
        }
        view.Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri("/CORDON;component/Themes/SharedStyles.xaml", UriKind.RelativeOrAbsolute)
        });

        var progressBar = new ProgressBar
        {
            Value = 42,
            Style = (Style)view.FindResource("ModArchiveProgressBarStyle"),
            Margin = new Thickness(0, 6, 0, 0)
        };
        var content = new StackPanel();
        content.Children.Add(new TextBlock
        {
            Text = "Распаковка test.7z: 42% · 420,0 МБ / 1,0 ГБ · осталось ~35 с"
        });
        content.Children.Add(progressBar);
        var panel = new Border
        {
            Style = (Style)view.FindResource("ModArchiveProgressPanelStyle"),
            Child = content
        };
        view.Children.Add(panel);

        view.Measure(new Size(940, 64));
        view.Arrange(new Rect(0, 0, 940, 64));
        view.UpdateLayout();

        Assert.Equal(Visibility.Visible, progressBar.Visibility);
        Assert.Equal(42, progressBar.Value);
        Assert.Equal(
            usePdaTheme ? Color.FromRgb(0xCF, 0x96, 0x2C) : Color.FromRgb(0xC9, 0x8A, 0x2E),
            Assert.IsType<SolidColorBrush>(progressBar.Foreground).Color);

        var bitmap = new RenderTargetBitmap(940, 64, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(view);
        Assert.Equal(940, bitmap.PixelWidth);
    }

    private static string FindProjectRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, "src", "StalkerModLauncher");
            if (File.Exists(Path.Combine(candidate, "StalkerModLauncher.csproj")))
            {
                return candidate;
            }
        }

        throw new DirectoryNotFoundException("StalkerModLauncher project root was not found.");
    }
}

using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using StalkerModLauncher.Views.Controls;
using Xunit;

namespace StalkerModLauncher.Tests;

public sealed class ConflictExplorerUiTests
{
    [Fact]
    public void ConflictExplorer_RendersInClassicAndEmbeddedPdaThemes()
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
        var view = new ConflictExplorerContentView
        {
            UsePdaTheme = usePdaTheme,
            CloseButtonText = usePdaTheme ? "Назад" : "Закрыть"
        };
        view.Measure(new Size(980, 640));
        view.Arrange(new Rect(0, 0, 980, 640));
        view.UpdateLayout();

        var bitmap = new RenderTargetBitmap(980, 640, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(view);
        Assert.Equal(980, bitmap.PixelWidth);
        Assert.Equal(640, bitmap.PixelHeight);
    }
}

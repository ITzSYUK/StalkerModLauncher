using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using StalkerModLauncher.Views.Controls;
using Xunit;

namespace StalkerModLauncher.Tests;

public sealed class ClassicProfileLayoutUiTests
{
    [Fact]
    public void ProfileOverview_RendersThreeFolderActionsAtClassicContentWidth()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var view = new ClassicProfileOverviewBar();
                view.Measure(new Size(960, 58));
                view.Arrange(new Rect(0, 0, 960, 58));
                view.UpdateLayout();

                var bitmap = new RenderTargetBitmap(960, 58, 96, 96, PixelFormats.Pbgra32);
                bitmap.Render(view);

                Assert.Equal(960, bitmap.PixelWidth);
                Assert.Equal(3, CountVisualChildren<Button>(view));
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

    private static int CountVisualChildren<T>(DependencyObject parent) where T : DependencyObject
    {
        var count = 0;
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);
            if (child is T)
            {
                count++;
            }

            count += CountVisualChildren<T>(child);
        }

        return count;
    }
}

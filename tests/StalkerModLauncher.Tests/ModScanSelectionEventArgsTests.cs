using StalkerModLauncher.ViewModels;
using Xunit;

namespace StalkerModLauncher.Tests;

public sealed class ModScanSelectionEventArgsTests
{
    [Fact]
    public async Task AcceptCompletesWithSelectedMods()
    {
        var first = new SelectableMod { Name = "First" };
        var second = new SelectableMod { Name = "Second" };
        var request = new ModScanSelectionEventArgs([first, second]);

        request.Accept([second]);

        var result = await request.Completion;
        Assert.NotNull(result);
        Assert.Same(second, Assert.Single(result));
    }

    [Fact]
    public async Task CancelCompletesWithoutSelection()
    {
        var request = new ModScanSelectionEventArgs([]);

        request.Cancel();

        Assert.Null(await request.Completion);
    }
}

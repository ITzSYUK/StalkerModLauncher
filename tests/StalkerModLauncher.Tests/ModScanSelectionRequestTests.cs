using StalkerModLauncher.ViewModels;
using Xunit;

namespace StalkerModLauncher.Tests;

public sealed class ModScanSelectionRequestTests
{
    [Fact]
    public async Task Accept_CompletesWithSelectedMods()
    {
        var first = new SelectableMod { Name = "First" };
        var second = new SelectableMod { Name = "Second" };
        var request = new ModScanSelectionRequest([first, second]);

        request.Accept([second]);

        var result = await request.Completion;
        Assert.NotNull(result);
        Assert.Same(second, Assert.Single(result));
    }

    [Fact]
    public async Task Cancel_CompletesWithoutSelection()
    {
        var request = new ModScanSelectionRequest([]);

        request.Cancel();

        Assert.Null(await request.Completion);
    }
}

using StalkerModLauncher.Services;
using Xunit;

namespace StalkerModLauncher.Tests;

public sealed class UiSoundResourcesTests
{
    [Theory]
    [InlineData("StalkerModLauncher.Resources.Sounds.pda_btn_press.ogg")]
    [InlineData("StalkerModLauncher.Resources.Sounds.pda_guide.ogg")]
    [InlineData("StalkerModLauncher.Resources.Sounds.pda_guide_2.ogg")]
    public void Assembly_ContainsUiSoundResource(string resourceName)
    {
        using var stream = typeof(UiSoundService).Assembly.GetManifestResourceStream(resourceName);

        Assert.NotNull(stream);
        Assert.True(stream.Length > 0);
    }
}

using StalkerModLauncher.Services;
using Xunit;

namespace StalkerModLauncher.Tests;

public sealed class WorkspaceFileStrategyTests
{
    [Fact]
    public void MustCopy_ReturnsTrueForFsgameThatLauncherRewrites()
    {
        Assert.True(WorkspaceFileStrategy.MustCopy("fsgame.ltx"));
    }

    [Theory]
    [InlineData("gamedata.db0")]
    [InlineData("gamedata/textures/texture.dds")]
    [InlineData("gamedata/config/system.ltx")]
    [InlineData("gamedata/scripts/test.script")]
    [InlineData("gamedata/config/localization.xml")]
    [InlineData("appdata/user.ltx")]
    [InlineData("userdata/logs/xray.log")]
    [InlineData("bin/xr_3da.exe")]
    [InlineData("bin/xrCore.dll")]
    public void MustCopy_ReturnsFalseForFilesThatCanBeLinked(string path)
    {
        Assert.False(WorkspaceFileStrategy.MustCopy(path));
    }
}

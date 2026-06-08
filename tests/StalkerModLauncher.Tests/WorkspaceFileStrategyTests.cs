using StalkerModLauncher.Services;
using Xunit;

namespace StalkerModLauncher.Tests;

public sealed class WorkspaceFileStrategyTests
{
    [Theory]
    [InlineData("fsgame.ltx")]
    [InlineData("gamedata/config/system.ltx")]
    [InlineData("gamedata/scripts/test.script")]
    [InlineData("appdata/user.ltx")]
    [InlineData("userdata/logs/xray.log")]
    public void MustCopy_ReturnsTrueForPotentiallyWritableFiles(string path)
    {
        Assert.True(WorkspaceFileStrategy.MustCopy(path));
    }

    [Theory]
    [InlineData("gamedata.db0")]
    [InlineData("gamedata/textures/texture.dds")]
    [InlineData("bin/xr_3da.exe")]
    [InlineData("bin/xrCore.dll")]
    public void MustCopy_ReturnsFalseForLargeImmutableAssets(string path)
    {
        Assert.False(WorkspaceFileStrategy.MustCopy(path));
    }
}

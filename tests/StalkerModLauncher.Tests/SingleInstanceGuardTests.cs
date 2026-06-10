using StalkerModLauncher.Services;
using Xunit;

namespace StalkerModLauncher.Tests;

public sealed class SingleInstanceGuardTests
{
    [Fact]
    public void SecondGuardWithSameNameIsNotPrimary()
    {
        var name = $"StalkerModLauncherTests-{Guid.NewGuid():N}";
        using var first = new SingleInstanceGuard(name);
        using var second = new SingleInstanceGuard(name);

        Assert.True(first.IsPrimaryInstance);
        Assert.False(second.IsPrimaryInstance);
    }
}

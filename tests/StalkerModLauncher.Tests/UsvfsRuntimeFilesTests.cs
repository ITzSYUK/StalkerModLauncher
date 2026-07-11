using StalkerModLauncher.Services;
using Xunit;

namespace StalkerModLauncher.Tests;

public sealed class UsvfsRuntimeFilesTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "StalkerModLauncherUsvfsRuntimeFilesTests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public void Check_DistinguishesX64AndX86RuntimeReadiness()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, UsvfsRuntimeFiles.ControllerDllFileName), string.Empty);
        File.WriteAllText(Path.Combine(_root, UsvfsRuntimeFiles.X64ProxyFileName), string.Empty);

        var x64Only = UsvfsRuntimeFiles.Check(_root);

        Assert.True(x64Only.IsReadyFor(WindowsExecutableArchitecture.X64));
        Assert.False(x64Only.IsReadyFor(WindowsExecutableArchitecture.X86));

        File.WriteAllText(Path.Combine(_root, UsvfsRuntimeFiles.X86DllFileName), string.Empty);
        File.WriteAllText(Path.Combine(_root, UsvfsRuntimeFiles.X86HostFileName), string.Empty);

        var complete = UsvfsRuntimeFiles.Check(_root);

        Assert.True(complete.IsReadyFor(WindowsExecutableArchitecture.X64));
        Assert.True(complete.IsReadyFor(WindowsExecutableArchitecture.X86));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}

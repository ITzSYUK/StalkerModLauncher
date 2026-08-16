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
    public void CheckRequiresCompleteValidatedRuntime()
    {
        Directory.CreateDirectory(_root);
        CopyRuntimeFile(UsvfsRuntimeFiles.ControllerDllFileName, WindowsExecutableArchitecture.X64);
        CopyRuntimeFile(UsvfsRuntimeFiles.X64ProxyFileName, WindowsExecutableArchitecture.X64);

        var x64Only = UsvfsRuntimeFiles.Check(_root);

        Assert.False(x64Only.IsReadyFor(WindowsExecutableArchitecture.X64));
        Assert.False(x64Only.IsReadyFor(WindowsExecutableArchitecture.X86));

        CopyRuntimeFile(UsvfsRuntimeFiles.X86DllFileName, WindowsExecutableArchitecture.X86);
        CopyRuntimeFile(UsvfsRuntimeFiles.X86HostFileName, WindowsExecutableArchitecture.X86);
        var missingProxy = UsvfsRuntimeFiles.Check(_root);

        Assert.False(missingProxy.IsReady);
        Assert.Contains(UsvfsRuntimeFiles.X86ProxyFileName, missingProxy.MissingFileNames);

        CopyRuntimeFile(UsvfsRuntimeFiles.X86ProxyFileName, WindowsExecutableArchitecture.X86);
        var complete = UsvfsRuntimeFiles.Check(_root);

        Assert.True(complete.IsReadyFor(WindowsExecutableArchitecture.X64));
        Assert.True(complete.IsReadyFor(WindowsExecutableArchitecture.X86));
        Assert.NotNull(complete.RuntimeVersion);
        Assert.Empty(complete.ValidationErrors);
    }

    [Fact]
    public void CheckRejectsWrongRuntimeArchitecture()
    {
        Directory.CreateDirectory(_root);
        CopyRuntimeFile(UsvfsRuntimeFiles.ControllerDllFileName, WindowsExecutableArchitecture.X64);
        CopyRuntimeFile(UsvfsRuntimeFiles.X64ProxyFileName, WindowsExecutableArchitecture.X64);
        CopyRuntimeFile(UsvfsRuntimeFiles.X86DllFileName, WindowsExecutableArchitecture.X86);
        CopyRuntimeFile(UsvfsRuntimeFiles.X86ProxyFileName, WindowsExecutableArchitecture.X64);
        CopyRuntimeFile(UsvfsRuntimeFiles.X86HostFileName, WindowsExecutableArchitecture.X86);

        var status = UsvfsRuntimeFiles.Check(_root);

        Assert.False(status.IsReady);
        Assert.Contains(
            status.ValidationErrors,
            error => error.Contains(UsvfsRuntimeFiles.X86ProxyFileName) && error.Contains("ожидалась архитектура x86"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private void CopyRuntimeFile(string fileName, WindowsExecutableArchitecture architecture)
    {
        var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var systemDirectory = architecture == WindowsExecutableArchitecture.X86 ? "SysWOW64" : "System32";
        var source = Path.Combine(windows, systemDirectory, "cmd.exe");
        Assert.True(File.Exists(source));
        File.Copy(source, Path.Combine(_root, fileName), overwrite: true);
    }
}

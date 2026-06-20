using StalkerModLauncher.Models;
using StalkerModLauncher.Services;
using Xunit;

namespace StalkerModLauncher.Tests;

public sealed class ProfileTransferServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "StalkerModLauncherTests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public void ExportThenImport_PreservesPortableProfileSettings()
    {
        Directory.CreateDirectory(_root);
        var filePath = Path.Combine(_root, "profile.stalkerprofile");
        var source = new ModProfile
        {
            Name = "Zona",
            IsStandalone = true,
            IsDiscordStatusEnabled = false,
            ExecutableRelativePath = @"bin_x64\xrEngine.exe",
            LaunchArguments = "-nointro",
            GameInstallPath = @"D:\Games\Stalker"
        };
        source.Mods.Add(new ModEntry
        {
            Name = "Main",
            SourcePath = @"D:\Mods\Zona",
            IsEnabled = true,
            Order = 1
        });
        var service = new ProfileTransferService();

        service.Export(filePath, source);
        var imported = service.Import(filePath);

        Assert.Equal(source.Name, imported.Name);
        Assert.False(imported.IsDiscordStatusEnabled);
        Assert.Equal(source.ExecutableRelativePath, imported.ExecutableRelativePath);
        Assert.Equal(source.Mods[0].SourcePath, imported.Mods[0].SourcePath);
        Assert.Equal(1, imported.Mods[0].Order);
        Assert.NotEqual(source.Id, imported.Id);
        Assert.Empty(imported.WorkspacePath);
    }

    [Fact]
    public void Import_RejectsUnsafeExecutablePath()
    {
        Directory.CreateDirectory(_root);
        var filePath = Path.Combine(_root, "unsafe.stalkerprofile");
        File.WriteAllText(
            filePath,
            """
            {
              "name": "Unsafe",
              "executableRelativePath": "..\\outside.exe",
              "mods": []
            }
            """);
        var service = new ProfileTransferService();

        Assert.Throws<InvalidDataException>(() => service.Import(filePath));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}

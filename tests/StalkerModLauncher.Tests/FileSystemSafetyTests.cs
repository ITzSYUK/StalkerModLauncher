using StalkerModLauncher.Services;
using Xunit;

namespace StalkerModLauncher.Tests;

public sealed class FileSystemSafetyTests
{
    [Theory]
    [InlineData(@"bin\xrEngine.exe")]
    [InlineData(@"folder.with..dots\game.exe")]
    public void ResolvePathInside_AcceptsSafeRelativePaths(string relativePath)
    {
        var root = Path.Combine(Path.GetTempPath(), "StalkerModLauncherTests", Guid.NewGuid().ToString("N"));

        var resolved = FileSystemSafety.ResolvePathInside(root, relativePath, "Executable");

        Assert.True(FileSystemSafety.IsDirectoryInside(resolved, root));
    }

    [Theory]
    [InlineData(@"..\outside.exe")]
    [InlineData(@"bin\..\..\outside.exe")]
    [InlineData(@"C:\Windows\notepad.exe")]
    public void ResolvePathInside_RejectsPathsOutsideRoot(string relativePath)
    {
        var root = Path.Combine(Path.GetTempPath(), "StalkerModLauncherTests", Guid.NewGuid().ToString("N"));

        Assert.Throws<InvalidOperationException>(
            () => FileSystemSafety.ResolvePathInside(root, relativePath, "Executable"));
    }

    [Fact]
    public void DeleteDirectoryContents_DeletesManagedDirectory()
    {
        var root = Path.Combine(Path.GetTempPath(), "StalkerModLauncherTests", Guid.NewGuid().ToString("N"));
        var child = Path.Combine(root, "profile");
        Directory.CreateDirectory(child);
        File.WriteAllText(Path.Combine(child, "file.txt"), "test");

        FileSystemSafety.DeleteDirectoryContents(child, root);

        Assert.False(Directory.Exists(child));
    }

    [Fact]
    public void DeleteDirectoryContents_RejectsDirectoryOutsideAllowedRoot()
    {
        var allowedRoot = Path.Combine(Path.GetTempPath(), "StalkerModLauncherTests", Guid.NewGuid().ToString("N"));
        var outside = Path.Combine(Path.GetTempPath(), "StalkerModLauncherTests", Guid.NewGuid().ToString("N"));

        Assert.Throws<InvalidOperationException>(
            () => FileSystemSafety.DeleteDirectoryContents(outside, allowedRoot));
    }
}

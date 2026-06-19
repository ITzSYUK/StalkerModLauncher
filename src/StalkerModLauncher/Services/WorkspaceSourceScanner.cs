using System.Security.Cryptography;
using System.Text;
using StalkerModLauncher.Models;

namespace StalkerModLauncher.Services;

internal sealed class WorkspaceSourceScanner
{
    public WorkspaceSourceSnapshot Capture(string gamePath, ModProfile profile, CancellationToken cancellationToken)
    {
        var game = CaptureDirectory(gamePath, cancellationToken);
        var mods = new Dictionary<string, DirectorySnapshot>();
        foreach (var mod in profile.Mods.Where(mod => mod.IsEnabled))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!Directory.Exists(mod.SourcePath))
            {
                throw new DirectoryNotFoundException($"Mod folder was not found: {mod.SourcePath}");
            }

            mods.Add(mod.Id, CaptureDirectory(mod.SourcePath, cancellationToken));
        }

        return new WorkspaceSourceSnapshot(game, mods);
    }

    public string CreateBuildSignature(string formatVersion, ModProfile profile, WorkspaceSourceSnapshot snapshot)
    {
        var builder = new StringBuilder();
        builder.AppendLine(formatVersion);
        AppendDirectoryFingerprint(builder, snapshot.Game);
        builder.AppendLine(profile.ExecutableRelativePath);
        builder.AppendLine(profile.ExecutableSourcePath);
        builder.AppendLine(profile.IsStandalone ? "standalone" : "overlay");

        foreach (var mod in profile.Mods.OrderBy(mod => mod.Order))
        {
            builder.Append(mod.Order).Append('|')
                .Append(mod.IsEnabled).Append('|')
                .Append(Path.GetFullPath(mod.SourcePath)).AppendLine();

            if (snapshot.Mods.TryGetValue(mod.Id, out var modSnapshot))
            {
                AppendDirectoryFingerprint(builder, modSnapshot);
            }
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString())));
    }

    private static DirectorySnapshot CaptureDirectory(string directoryPath, CancellationToken cancellationToken)
    {
        var fullRoot = Path.GetFullPath(directoryPath);
        var directories = Directory.EnumerateDirectories(fullRoot, "*", SafeEnumerationOptions)
            .Select(path => Path.GetRelativePath(fullRoot, path))
            .ToArray();
        var files = Directory.EnumerateFiles(fullRoot, "*", SafeEnumerationOptions)
            .Select(path =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                var info = new FileInfo(path);
                return new SourceFileSnapshot(
                    path,
                    Path.GetRelativePath(fullRoot, path),
                    info.Length,
                    info.LastWriteTimeUtc.Ticks);
            })
            .OrderBy(file => file.RelativePath, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return new DirectorySnapshot(fullRoot, directories, files);
    }

    private static void AppendDirectoryFingerprint(StringBuilder builder, DirectorySnapshot snapshot)
    {
        builder.AppendLine(snapshot.RootPath);
        foreach (var file in snapshot.Files)
        {
            builder.Append(file.RelativePath).Append('|')
                .Append(file.Length).Append('|')
                .Append(file.LastWriteTimeUtcTicks).AppendLine();
        }
    }

    private static EnumerationOptions SafeEnumerationOptions { get; } = new()
    {
        RecurseSubdirectories = true,
        IgnoreInaccessible = false,
        AttributesToSkip = FileAttributes.ReparsePoint
    };
}

internal sealed record WorkspaceSourceSnapshot(
    DirectorySnapshot Game,
    IReadOnlyDictionary<string, DirectorySnapshot> Mods);

internal sealed record DirectorySnapshot(
    string RootPath,
    IReadOnlyList<string> Directories,
    IReadOnlyList<SourceFileSnapshot> Files);

internal sealed record SourceFileSnapshot(
    string FullPath,
    string RelativePath,
    long Length,
    long LastWriteTimeUtcTicks);

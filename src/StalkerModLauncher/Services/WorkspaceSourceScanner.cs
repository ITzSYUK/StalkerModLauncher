using System.Security.Cryptography;
using System.Text;
using StalkerModLauncher.Models;

namespace StalkerModLauncher.Services;

internal static class WorkspaceSourceScanner
{
    public static WorkspaceSourceSnapshot Capture(FileLayerPlan plan, CancellationToken cancellationToken)
    {
        var game = CaptureDirectory(plan.BaseGame.RootPath, cancellationToken);
        var mods = new Dictionary<string, DirectorySnapshot>();
        foreach (var layer in plan.Mods)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!Directory.Exists(layer.RootPath))
            {
                throw new DirectoryNotFoundException($"Mod folder was not found: {layer.RootPath}");
            }

            mods.Add(layer.Id, CaptureDirectory(layer.RootPath, cancellationToken));
        }

        return new WorkspaceSourceSnapshot(game, mods);
    }

    public static WorkspaceSourceSnapshot Capture(string gamePath, ModProfile profile, CancellationToken cancellationToken)
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

    public static string CreateBuildSignature(
        string formatVersion,
        ModProfile profile,
        WorkspaceSourceSnapshot snapshot,
        FileLayerPlan plan)
    {
        var builder = new StringBuilder();
        builder.AppendLine(formatVersion);
        foreach (var layer in plan.SourceLayers)
        {
            builder.Append(layer.Kind).Append('|')
                .Append(layer.Order).Append('|')
                .Append(layer.Id).Append('|')
                .Append(layer.RootPath).AppendLine();
        }

        AppendDirectoryFingerprint(builder, snapshot.Game);
        builder.AppendLine(profile.ExecutableRelativePath);
        builder.AppendLine(profile.ExecutableSourcePath);
        builder.AppendLine(profile.IsStandalone ? "standalone" : "overlay");

        foreach (var layer in plan.Mods)
        {
            var mod = layer.Mod!;
            builder.Append(layer.Order).Append('|')
                .Append(mod.IsEnabled).Append('|')
                .Append(layer.RootPath).AppendLine();
            foreach (var excluded in mod.ExcludedFiles.OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
            {
                builder.Append("excluded|").AppendLine(excluded);
            }

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
        var directories = new List<string>();
        var files = new List<SourceFileSnapshot>();
        var root = new DirectoryInfo(fullRoot);

        foreach (var entry in root.EnumerateFileSystemInfos("*", SafeEnumerationOptions))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var relativePath = Path.GetRelativePath(fullRoot, entry.FullName);
            if (entry is DirectoryInfo)
            {
                directories.Add(relativePath);
                continue;
            }

            if (entry is FileInfo file)
            {
                files.Add(new SourceFileSnapshot(
                    file.FullName,
                    relativePath,
                    file.Length,
                    file.LastWriteTimeUtc.Ticks));
            }
        }

        return new DirectorySnapshot(
            fullRoot,
            directories,
            files.OrderBy(file => file.RelativePath, StringComparer.OrdinalIgnoreCase).ToArray());
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

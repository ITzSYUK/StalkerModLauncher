using StalkerModLauncher.Models;

namespace StalkerModLauncher.Services;

public sealed class VirtualFileResolver
{
    private readonly OverlayDiagnosticsService _diagnostics = new();

    public VirtualFileResolution ResolveRead(
        FileLayerPlan layerPlan,
        OverlayManifest manifest,
        string relativePath)
    {
        FileSystemSafety.EnsureRelativePath(relativePath, "Virtual file");
        var normalizedRelativePath = NormalizeRelativePath(relativePath);

        var writeTarget = _diagnostics.ResolveWriteTarget(manifest, normalizedRelativePath);
        if (File.Exists(writeTarget.StoragePath))
        {
            return new VirtualFileResolution(
                normalizedRelativePath,
                writeTarget.StoragePath,
                writeTarget.Kind == OverlayWriteTargetKind.KnownWritableFile
                    ? VirtualFileSourceKind.KnownWritableFile
                    : VirtualFileSourceKind.Overwrite,
                writeTarget.Kind == OverlayWriteTargetKind.KnownWritableFile
                    ? "профильные writable-файлы"
                    : "профильный overwrite");
        }

        var finalFile = layerPlan.FindFinalFile(normalizedRelativePath);
        if (finalFile is null)
        {
            return new VirtualFileResolution(
                normalizedRelativePath,
                null,
                VirtualFileSourceKind.Missing,
                "файл не найден");
        }

        return new VirtualFileResolution(
            normalizedRelativePath,
            finalFile.FullPath,
            VirtualFileSourceKind.Layer,
            finalFile.SourceName,
            finalFile.Layer.Kind,
            finalFile.Layer.Id,
            finalFile.Layer.Order);
    }

    public VirtualWriteResolution ResolveWrite(OverlayManifest manifest, string relativePath)
    {
        var target = _diagnostics.ResolveWriteTarget(manifest, relativePath);
        return new VirtualWriteResolution(
            target.RelativePath,
            target.StoragePath,
            target.Kind,
            target.Reason);
    }

    public IReadOnlyList<VirtualDirectoryEntry> EnumerateDirectory(
        FileLayerPlan layerPlan,
        OverlayManifest manifest,
        string relativeDirectory)
    {
        FileSystemSafety.EnsureRelativePath(relativeDirectory, "Virtual directory");
        var normalizedRelativeDirectory = NormalizeRelativePath(relativeDirectory);
        var entries = new Dictionary<string, VirtualDirectoryEntry>(StringComparer.OrdinalIgnoreCase);

        foreach (var layer in layerPlan.SourceLayers.Where(layer => Directory.Exists(layer.RootPath)))
        {
            AddLayerDirectoryEntries(entries, layer, normalizedRelativeDirectory);
        }

        AddDirectoryEntries(
            entries,
            manifest.WriteOverlayRoot,
            normalizedRelativeDirectory,
            VirtualFileSourceKind.Overwrite,
            "профильный overwrite");

        foreach (var writableFile in manifest.WritableFiles)
        {
            AddKnownWritableEntry(entries, writableFile, normalizedRelativeDirectory);
        }

        return entries.Values
            .OrderBy(entry => entry.IsDirectory ? 0 : 1)
            .ThenBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static void AddLayerDirectoryEntries(
        IDictionary<string, VirtualDirectoryEntry> entries,
        FileLayer layer,
        string relativeDirectory)
    {
        var directory = Path.Combine(layer.RootPath, relativeDirectory);
        if (!Directory.Exists(directory))
        {
            return;
        }

        foreach (var child in Directory.EnumerateDirectories(directory))
        {
            AddOrReplace(entries, new VirtualDirectoryEntry(
                Path.GetFileName(child),
                NormalizeRelativePath(Path.GetRelativePath(layer.RootPath, child)),
                IsDirectory: true,
                VirtualFileSourceKind.Layer,
                FileLayerPlan.GetDisplayName(layer),
                layer.Kind,
                layer.Id,
                layer.Order));
        }

        foreach (var child in Directory.EnumerateFiles(directory))
        {
            AddOrReplace(entries, new VirtualDirectoryEntry(
                Path.GetFileName(child),
                NormalizeRelativePath(Path.GetRelativePath(layer.RootPath, child)),
                IsDirectory: false,
                VirtualFileSourceKind.Layer,
                FileLayerPlan.GetDisplayName(layer),
                layer.Kind,
                layer.Id,
                layer.Order));
        }
    }

    private static void AddDirectoryEntries(
        IDictionary<string, VirtualDirectoryEntry> entries,
        string root,
        string relativeDirectory,
        VirtualFileSourceKind sourceKind,
        string sourceName)
    {
        var directory = Path.Combine(root, relativeDirectory);
        if (!Directory.Exists(directory))
        {
            return;
        }

        foreach (var child in Directory.EnumerateDirectories(directory))
        {
            AddOrReplace(entries, new VirtualDirectoryEntry(
                Path.GetFileName(child),
                NormalizeRelativePath(Path.GetRelativePath(root, child)),
                IsDirectory: true,
                sourceKind,
                sourceName));
        }

        foreach (var child in Directory.EnumerateFiles(directory))
        {
            AddOrReplace(entries, new VirtualDirectoryEntry(
                Path.GetFileName(child),
                NormalizeRelativePath(Path.GetRelativePath(root, child)),
                IsDirectory: false,
                sourceKind,
                sourceName));
        }
    }

    private static void AddKnownWritableEntry(
        IDictionary<string, VirtualDirectoryEntry> entries,
        OverlayWritableFileSnapshot writableFile,
        string relativeDirectory)
    {
        if (!File.Exists(writableFile.StoragePath))
        {
            return;
        }

        var fileDirectory = Path.GetDirectoryName(writableFile.RelativePath) ?? string.Empty;
        if (!PathEquals(fileDirectory, relativeDirectory))
        {
            return;
        }

        AddOrReplace(entries, new VirtualDirectoryEntry(
            Path.GetFileName(writableFile.RelativePath),
            NormalizeRelativePath(writableFile.RelativePath),
            IsDirectory: false,
            VirtualFileSourceKind.KnownWritableFile,
            "профильные writable-файлы"));
    }

    private static void AddOrReplace(
        IDictionary<string, VirtualDirectoryEntry> entries,
        VirtualDirectoryEntry entry)
    {
        entries[entry.Name] = entry;
    }

    private static bool PathEquals(string left, string right)
    {
        return string.Equals(
            NormalizeRelativePath(left).TrimEnd(Path.DirectorySeparatorChar),
            NormalizeRelativePath(right).TrimEnd(Path.DirectorySeparatorChar),
            StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeRelativePath(string relativePath)
    {
        return relativePath.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
    }
}

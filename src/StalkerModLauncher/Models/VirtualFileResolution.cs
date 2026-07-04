namespace StalkerModLauncher.Models;

public enum VirtualFileSourceKind
{
    Missing,
    Layer,
    KnownWritableFile,
    Overwrite
}

public sealed record VirtualFileResolution(
    string RelativePath,
    string? PhysicalPath,
    VirtualFileSourceKind SourceKind,
    string SourceName,
    FileLayerKind? LayerKind = null,
    string? LayerId = null,
    int? Order = null)
{
    public bool Exists => SourceKind is not VirtualFileSourceKind.Missing && !string.IsNullOrWhiteSpace(PhysicalPath);
}

public sealed record VirtualWriteResolution(
    string RelativePath,
    string PhysicalPath,
    OverlayWriteTargetKind TargetKind,
    string Reason);

public sealed record VirtualDirectoryEntry(
    string Name,
    string RelativePath,
    bool IsDirectory,
    VirtualFileSourceKind SourceKind,
    string SourceName,
    FileLayerKind? LayerKind = null,
    string? LayerId = null,
    int? Order = null);

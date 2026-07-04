namespace StalkerModLauncher.Models;

public enum OverlayWriteTargetKind
{
    KnownWritableFile,
    DefaultOverwrite
}

public sealed record OverlayFileProviderSnapshot(
    FileLayerKind LayerKind,
    string LayerId,
    string LayerName,
    string FullPath,
    string RelativePath,
    string SourceName,
    int Order);

public sealed record OverlayWriteTargetSnapshot(
    OverlayWriteTargetKind Kind,
    string RelativePath,
    string StoragePath,
    string Reason);

public sealed record OverlayFileDiagnostic(
    string RelativePath,
    OverlayFileProviderSnapshot? VisibleFile,
    IReadOnlyList<OverlayFileProviderSnapshot> Providers,
    OverlayWriteTargetSnapshot WriteTarget)
{
    public bool Exists => VisibleFile is not null;
}

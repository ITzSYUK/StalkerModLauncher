namespace StalkerModLauncher.Models;

public enum FileLayerKind
{
    BaseGame,
    Mod,
    UserData
}

public sealed record FileLayer(
    FileLayerKind Kind,
    string Id,
    string Name,
    string RootPath,
    int Order,
    ModEntry? Mod = null);

public sealed record FileLayerFile(
    string FullPath,
    string RelativePath,
    string SourceName,
    FileLayer Layer);

public sealed record FileLayerExecutableCandidate(
    string FullPath,
    string RelativePath,
    string SourceName,
    FileLayer Layer);

public sealed record FileLayerOverwrite(
    string RelativePath,
    FileLayerFile ReplacedFile,
    FileLayerFile ReplacingFile);

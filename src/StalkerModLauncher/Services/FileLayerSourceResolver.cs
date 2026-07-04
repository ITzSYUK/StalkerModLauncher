using StalkerModLauncher.Models;

namespace StalkerModLauncher.Services;

internal sealed record FileLayerSource(
    string FullPath,
    string RelativePath,
    string SourceName,
    FileLayer Layer);

internal static class FileLayerSourceResolver
{
    public static IReadOnlyList<LaunchExecutableSearchRoot> CreateExecutableRoots(FileLayerPlan plan)
    {
        return plan.SourceLayers
            .Select(layer => new LaunchExecutableSearchRoot(layer.RootPath, GetDisplayName(layer), layer.Order))
            .ToArray();
    }

    public static FileLayerSource? FindFinalSource(FileLayerPlan plan, string relativePath)
    {
        FileSystemSafety.EnsureRelativePath(relativePath, "Layer file");
        return plan.SourceLayers
            .Where(layer => Directory.Exists(layer.RootPath))
            .Select(layer => new FileLayerSource(
                Path.Combine(layer.RootPath, relativePath),
                relativePath,
                GetDisplayName(layer),
                layer))
            .LastOrDefault(source => File.Exists(source.FullPath));
    }

    public static string GetDisplayName(FileLayer layer)
    {
        return layer.Kind switch
        {
            FileLayerKind.BaseGame => "базовая игра",
            FileLayerKind.Mod => $"мод: {layer.Name}",
            FileLayerKind.UserData => "данные профиля",
            _ => layer.Name
        };
    }
}

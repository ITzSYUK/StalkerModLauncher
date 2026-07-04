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
        return plan.CreateExecutableRoots();
    }

    public static FileLayerSource? FindFinalSource(FileLayerPlan plan, string relativePath)
    {
        var source = plan.FindFinalFile(relativePath);
        return source is null
            ? null
            : new FileLayerSource(source.FullPath, source.RelativePath, source.SourceName, source.Layer);
    }

    public static string GetDisplayName(FileLayer layer)
    {
        return FileLayerPlan.GetDisplayName(layer);
    }
}

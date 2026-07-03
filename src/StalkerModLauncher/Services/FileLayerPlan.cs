using StalkerModLauncher.Models;

namespace StalkerModLauncher.Services;

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

public sealed class FileLayerPlan
{
    private FileLayerPlan(IReadOnlyList<FileLayer> layers)
    {
        Layers = layers;
        BaseGame = layers.Single(layer => layer.Kind == FileLayerKind.BaseGame);
        UserData = layers.Single(layer => layer.Kind == FileLayerKind.UserData);
        Mods = layers
            .Where(layer => layer.Kind == FileLayerKind.Mod)
            .OrderBy(layer => layer.Order)
            .ToArray();
    }

    public IReadOnlyList<FileLayer> Layers { get; }

    public FileLayer BaseGame { get; }

    public IReadOnlyList<FileLayer> Mods { get; }

    public FileLayer UserData { get; }

    public IEnumerable<FileLayer> SourceLayers => new[] { BaseGame }.Concat(Mods);

    public static FileLayerPlan CreateLinkedWorkspace(string gamePath, ModProfile profile, string workspaceRoot)
    {
        if (profile.IsStandalone)
        {
            throw new InvalidOperationException("FileLayerPlan for standalone profiles is not part of the linked workspace pipeline.");
        }

        var layers = new List<FileLayer>
        {
            new(
                FileLayerKind.BaseGame,
                "__base_game",
                "Base game",
                Path.GetFullPath(gamePath),
                0)
        };

        layers.AddRange(profile.Mods
            .Where(mod => mod.IsEnabled)
            .OrderBy(mod => mod.Order)
            .Select(mod => new FileLayer(
                FileLayerKind.Mod,
                mod.Id,
                mod.Name,
                Path.GetFullPath(mod.SourcePath),
                mod.Order,
                mod)));

        layers.Add(new FileLayer(
            FileLayerKind.UserData,
            "__userdata",
            "Profile user data",
            Path.Combine(Path.GetFullPath(workspaceRoot), "userdata"),
            int.MaxValue));

        return new FileLayerPlan(layers);
    }
}

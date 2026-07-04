using StalkerModLauncher.Models;

namespace StalkerModLauncher.Services;

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

    public IReadOnlyList<LaunchExecutableSearchRoot> CreateExecutableRoots()
    {
        return SourceLayers
            .Select(layer => new LaunchExecutableSearchRoot(layer.RootPath, GetDisplayName(layer), layer.Order))
            .ToArray();
    }

    public FileLayerFile? FindFinalFile(string relativePath)
    {
        return FindAllProviders(relativePath).LastOrDefault();
    }

    public IReadOnlyList<FileLayerFile> FindAllProviders(string relativePath)
    {
        FileSystemSafety.EnsureRelativePath(relativePath, "Layer file");
        return SourceLayers
            .Where(layer => Directory.Exists(layer.RootPath))
            .Select(layer => new FileLayerFile(
                Path.Combine(layer.RootPath, relativePath),
                relativePath,
                GetDisplayName(layer),
                layer))
            .Where(source => File.Exists(source.FullPath))
            .ToArray();
    }

    public IReadOnlyList<FileLayerExecutableCandidate> GetExecutableCandidates(CancellationToken cancellationToken = default)
    {
        var candidates = new List<FileLayerExecutableCandidate>();
        foreach (var layer in SourceLayers.Where(layer => Directory.Exists(layer.RootPath)))
        {
            IEnumerable<string> files;
            try
            {
                files = Directory.EnumerateFiles(layer.RootPath, "*.exe", SafeEnumerationOptions);
            }
            catch
            {
                continue;
            }

            foreach (var file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();
                candidates.Add(new FileLayerExecutableCandidate(
                    file,
                    Path.GetRelativePath(layer.RootPath, file),
                    GetDisplayName(layer),
                    layer));
            }
        }

        return candidates
            .OrderBy(candidate => candidate.Layer.Order)
            .ThenBy(candidate => candidate.RelativePath, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public IReadOnlyList<FileLayerOverwrite> GetOverwrittenFiles(FileLayer replacingLayer, CancellationToken cancellationToken = default)
    {
        if (replacingLayer.Kind is not FileLayerKind.Mod)
        {
            return [];
        }

        var previousFiles = new Dictionary<string, FileLayerFile>(StringComparer.OrdinalIgnoreCase);
        foreach (var layer in SourceLayers.Where(layer => layer.Order < replacingLayer.Order && Directory.Exists(layer.RootPath)))
        {
            foreach (var file in EnumerateLayerFiles(layer, cancellationToken))
            {
                previousFiles[file.RelativePath] = file;
            }
        }

        var overwrites = new List<FileLayerOverwrite>();
        foreach (var replacingFile in EnumerateLayerFiles(replacingLayer, cancellationToken))
        {
            if (previousFiles.TryGetValue(replacingFile.RelativePath, out var replacedFile))
            {
                overwrites.Add(new FileLayerOverwrite(replacingFile.RelativePath, replacedFile, replacingFile));
            }
        }

        return overwrites
            .OrderBy(overwrite => overwrite.RelativePath, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public IReadOnlyList<FileLayerOverwrite> GetOverwrittenFiles(ModEntry mod, CancellationToken cancellationToken = default)
    {
        var layer = Mods.FirstOrDefault(layer => ReferenceEquals(layer.Mod, mod) || layer.Id == mod.Id);
        return layer is null ? [] : GetOverwrittenFiles(layer, cancellationToken);
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

    private static IEnumerable<FileLayerFile> EnumerateLayerFiles(FileLayer layer, CancellationToken cancellationToken)
    {
        IEnumerable<string> files;
        try
        {
            files = Directory.EnumerateFiles(layer.RootPath, "*", SafeEnumerationOptions);
        }
        catch
        {
            yield break;
        }

        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return new FileLayerFile(
                file,
                Path.GetRelativePath(layer.RootPath, file),
                GetDisplayName(layer),
                layer);
        }
    }

    private static EnumerationOptions SafeEnumerationOptions { get; } = new()
    {
        RecurseSubdirectories = true,
        IgnoreInaccessible = true,
        AttributesToSkip = FileAttributes.ReparsePoint
    };
}

using StalkerModLauncher.Models;

namespace StalkerModLauncher.Services;

internal sealed class UsvfsProfileDataPreparer
{
    private readonly ProfileDataConfigurator _dataConfigurator = new();

    public string? Prepare(
        FileLayerPlan layerPlan,
        OverlayManifest manifest,
        string profileWorkspace,
        IProgress<string>? progress = null)
    {
        var source = layerPlan.FindFinalFile("fsgame.ltx");
        if (source is null)
        {
            progress?.Report("USVFS warning: fsgame.ltx was not found in the enabled layers.");
            return null;
        }

        var profileDataPath = Path.Combine(profileWorkspace, "userdata");
        Directory.CreateDirectory(profileDataPath);
        _dataConfigurator.EnsureProfileUserLtx(
            layerPlan.BaseGame.RootPath,
            profileDataPath,
            progress);
        Directory.CreateDirectory(manifest.WriteOverlayRoot);

        var lines = File.ReadAllLines(source.FullPath, XRayTextEncoding.Config);
        var appDataLineFound = false;
        for (var index = 0; index < lines.Length; index++)
        {
            if (!lines[index].TrimStart().StartsWith("$app_data_root$", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            lines[index] = $"$app_data_root$ = true | false| {profileDataPath}";
            appDataLineFound = true;
            break;
        }

        if (!appDataLineFound)
        {
            progress?.Report("USVFS warning: fsgame.ltx has no $app_data_root$ entry.");
        }

        var destination = Path.Combine(manifest.WriteOverlayRoot, "fsgame.ltx");
        File.WriteAllLines(destination, lines, XRayTextEncoding.Config);
        progress?.Report($"USVFS profile fsgame.ltx prepared from {source.SourceName}.");
        return destination;
    }
}

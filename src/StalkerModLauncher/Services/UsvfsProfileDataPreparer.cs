using StalkerModLauncher.Models;

namespace StalkerModLauncher.Services;

internal sealed class UsvfsProfileDataPreparer
{
    private readonly ProfileDataConfigurator _dataConfigurator = new();
    private readonly ProfileShaderCacheSeeder _shaderCacheSeeder = new();

    public string? Prepare(
        FileLayerPlan layerPlan,
        OverlayManifest manifest,
        string profileWorkspace,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var source = layerPlan.FindFinalFile("fsgame.ltx");
        if (source is null)
        {
            throw new FileNotFoundException(
                "fsgame.ltx was not found in the enabled layers. " +
                "Profile-local saves and logs cannot be guaranteed.");
        }

        var profileDataPath = Path.Combine(profileWorkspace, "userdata");
        var destination = Path.Combine(manifest.WriteOverlayRoot, "fsgame.ltx");
        _dataConfigurator.WriteProfileFsgame(source.FullPath, destination, profileDataPath);
        Directory.CreateDirectory(profileDataPath);
        _dataConfigurator.EnsureProfileUserLtx(
            layerPlan,
            profileDataPath,
            progress);
        _shaderCacheSeeder.Seed(layerPlan, profileDataPath, progress, cancellationToken);
        progress?.Report($"USVFS profile fsgame.ltx prepared from {source.SourceName}.");
        return destination;
    }
}

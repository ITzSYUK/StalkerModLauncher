using StalkerModLauncher.Models;

namespace StalkerModLauncher.Services;

internal static class UsvfsProfileDataPreparer
{
    public static string? Prepare(
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
        ProfileDataConfigurator.WriteProfileFsgame(source.FullPath, destination, profileDataPath);
        Directory.CreateDirectory(profileDataPath);
        ProfileDataConfigurator.EnsureProfileUserLtx(
            layerPlan,
            profileDataPath,
            progress);
        ProfileShaderCacheSeeder.Seed(layerPlan, profileDataPath, progress, cancellationToken);
        progress?.Report($"USVFS profile fsgame.ltx prepared from {source.SourceName}.");
        return destination;
    }
}

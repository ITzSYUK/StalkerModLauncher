namespace StalkerModLauncher.Services;

public sealed record ProfileWritableGameFileRule(
    string RelativePath,
    string StorageRelativePath,
    string Reason);

public static class ProfileWritableGameFiles
{
    public const string WritableGameFilesRootRelativePath = @"userdata\writable-game-files";
    public const string DefaultOverwriteRootRelativePath = @"userdata\overwrite";

    public static IReadOnlyList<ProfileWritableGameFileRule> Rules { get; } =
    [
        new(
            Path.Combine("gamedata", "configs", "localization.ltx"),
            Path.Combine(WritableGameFilesRootRelativePath, "gamedata", "configs", "localization.ltx"),
            "Anomaly and some X-Ray builds write language selection into gamedata.")
    ];
}
